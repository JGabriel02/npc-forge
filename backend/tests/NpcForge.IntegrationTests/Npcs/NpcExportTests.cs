using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NpcForge.IntegrationTests.Npcs;

[Collection("api")]
public class NpcExportTests(ApiFactory factory)
{
    [Fact]
    public async Task Export_WithFilters_ReturnsOnlyMatchingNpcs_AllPages()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        for (var i = 0; i < 25; i++)
            await client.CreateNpcAsync($"NPC {i}", statBlock: i < 3 ? NpcTestData.ValidStatBlock() : null);

        var all = await ExportAsync(client, "");
        var withSheet = await ExportAsync(client, "?hasStatBlock=true");

        Assert.Equal(25, all.GetProperty("npcs").GetArrayLength());
        Assert.Equal(3, withSheet.GetProperty("npcs").GetArrayLength());
    }

    // A revisão final achou: exportar mais do que a importação aceita gerava um arquivo inútil na versão web.
    [Fact]
    public async Task Export_MoreThan500_Returns400WithMessage()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await ImportAsync(client, Enumerable.Range(0, 500).Select(i => (object)new { name = $"NPC {i}" }));
        await client.CreateNpcAsync("NPC 501");

        var response = await client.GetAsync("/api/npcs/export");

        await AssertProblem(response,
            "Esse filtro tem 501 NPCs, e um arquivo leva no máximo 500. Filtre por campanha ou tipo e exporte em partes.");
    }

    [Fact]
    public async Task Export_FileOver20MB_Returns400WithMessage()
    {
        // Cerca de 210 KB por NPC (fichas com as listas cheias): 120 NPCs passam de 20 MB.
        var client = await factory.CreateAuthenticatedClientAsync();
        var feature = new { name = new string('n', 100), description = new string('d', 2000) };
        var action = new { name = new string('n', 100), description = new string('d', 2000), damage = new string('x', 500) };
        var statBlock = NpcTestData.ValidStatBlock() with
        {
            Traits = Enumerable.Repeat(new FeaturePayload(feature.name, feature.description), 30).ToList(),
            Actions = Enumerable.Repeat(new ActionPayload(action.name, action.description, 5, action.damage), 30).ToList(),
            Reactions = Enumerable.Repeat(new FeaturePayload(feature.name, feature.description), 30).ToList(),
        };
        await ImportAsync(client, Enumerable.Range(0, 120).Select(i => (object)new { name = $"NPC {i}", statBlock }));

        var response = await client.GetAsync("/api/npcs/export");

        await AssertProblem(response,
            "O arquivo passaria de 20 MB. Filtre por campanha ou tipo e exporte em partes.");
    }

    private static async Task ImportAsync(HttpClient client, IEnumerable<object> npcs)
    {
        var response = await client.PostAsJsonAsync("/api/npcs/import", new { format = "npcforge-npcs", version = 1, npcs });
        response.EnsureSuccessStatusCode();
    }

    private static async Task AssertProblem(HttpResponseMessage response, string detail)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(detail, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Export_FileHasFormatVersionAndNoIdsOrCampaign()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var campaign = await client.CreateCampaignAsync();
        var created = await client.PostAsJsonAsync("/api/npcs", new
        {
            name = "Borin", campaignId = campaign, notes = "deve dinheiro", statBlock = NpcTestData.ValidStatBlock(),
        });
        created.EnsureSuccessStatusCode();

        var file = await ExportAsync(client, "");

        Assert.Equal("npcforge-npcs", file.GetProperty("format").GetString());
        Assert.Equal(1, file.GetProperty("version").GetInt32());
        Assert.True(file.TryGetProperty("exportedAt", out _));
        var npc = file.GetProperty("npcs")[0];
        foreach (var hidden in new[] { "id", "campaignId", "createdAt", "updatedAt", "userId" })
            Assert.False(npc.TryGetProperty(hidden, out _), $"o arquivo não deveria ter '{hidden}'");
        Assert.Equal("deve dinheiro", npc.GetProperty("notes").GetString());
        Assert.Equal("Martelo", npc.GetProperty("statBlock").GetProperty("actions")[0].GetProperty("name").GetString());
        Assert.False(npc.GetProperty("statBlock").TryGetProperty("modifiers", out _));
    }

    [Fact]
    public async Task Export_SetsAttachmentFileName()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var npc = await client.CreateNpcAsync("Ireena Kolyana!");

        var list = await client.GetAsync("/api/npcs/export");
        var one = await client.GetAsync($"/api/npcs/{npc.Id}/export");

        var listName = list.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        Assert.StartsWith("npcs-", listName);
        Assert.EndsWith(".json", listName);
        Assert.Equal("attachment", one.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("npc-ireena-kolyana.json", one.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task Export_InvalidCampaignFilter_ReturnsEmptyList()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await client.CreateNpcAsync("Borin");

        var file = await ExportAsync(client, "?campaignId=abc");

        Assert.Equal(0, file.GetProperty("npcs").GetArrayLength());
    }

    [Fact]
    public async Task ExportOne_OtherUsersNpc_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var npc = await owner.CreateNpcAsync();
        var intruder = await factory.CreateAuthenticatedClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/npcs/{npc.Id}/export")).StatusCode);
    }

    [Fact]
    public async Task Export_ExcludesOtherUsersNpcs()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var other = await factory.CreateAuthenticatedClientAsync();
        await client.CreateNpcAsync("Meu");
        await other.CreateNpcAsync("Alheio");

        var file = await ExportAsync(client, "");

        Assert.Equal(["Meu"], file.GetProperty("npcs").EnumerateArray().Select(n => n.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Cors_ExposesContentDisposition()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/npcs/export");
        request.Headers.Add("Origin", "http://localhost:4200");

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Expose-Headers", out var exposed));
        Assert.Contains("Content-Disposition", string.Join(",", exposed));
    }

    private static async Task<JsonElement> ExportAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/npcs/export{query}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
