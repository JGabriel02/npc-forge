using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NpcForge.IntegrationTests.Npcs;

[Collection("api")]
public class NpcImportTests(ApiFactory factory)
{
    [Fact]
    public async Task RoundTrip_ExportThenImportWithAnotherUser_CopiesEverythingWithoutCampaign()
    {
        var a = await factory.CreateAuthenticatedClientAsync();
        var campaign = await a.CreateCampaignAsync();
        var created = await a.PostAsJsonAsync("/api/npcs", new
        {
            name = "Borin", race = "anão", campaignId = campaign, notes = "deve dinheiro",
            statBlock = NpcTestData.ValidStatBlock(),
        });
        var borin = (await created.Content.ReadFromJsonAsync<NpcBody>())!;
        await a.CreateNpcAsync("Hilda", occupation: "taverneira");
        var file = await (await a.GetAsync("/api/npcs/export")).Content.ReadAsStringAsync();
        var b = await factory.CreateAuthenticatedClientAsync();

        var response = await ImportRawAsync(b, file);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imported").GetInt32());
        var list = (await b.GetFromJsonAsync<PagedBody<NpcSummaryBody>>("/api/npcs"))!.Items;
        Assert.Equal(2, list.Count);
        Assert.All(list, n => Assert.Null(n.CampaignId));
        Assert.DoesNotContain(list, n => n.Id == borin.Id);
        var copy = (await b.GetFromJsonAsync<NpcBody>($"/api/npcs/{list.Single(n => n.Name == "Borin").Id}"))!;
        Assert.Equal("anão", copy.Race);
        Assert.Equal("deve dinheiro", copy.Notes);
        Assert.Equal(borin.StatBlock!.Str, copy.StatBlock!.Str);
        Assert.Equal(borin.StatBlock.HitDice, copy.StatBlock.HitDice);
        Assert.Equal(borin.StatBlock.Actions, copy.StatBlock.Actions);
    }

    [Fact]
    public async Task Import_SameFileTwice_CreatesCopies()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var file = FileWith(Npc("Um"), Npc("Dois"));

        await ImportAsync(client, file);
        await ImportAsync(client, file);

        Assert.Equal(4, (await client.GetFromJsonAsync<PagedBody<NpcSummaryBody>>("/api/npcs"))!.TotalCount);
    }

    [Fact]
    public async Task Import_OneInvalidNpc_ImportsNothing()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var invalid = Npc("Rahadin", withStatBlock: true);
        invalid["statBlock"]!["str"] = 31;

        var response = await ImportAsync(client, FileWith(Npc("Um"), invalid, Npc("Três")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Contains(errors.EnumerateObject(), e => e.Name.Equals("npcs[1].statBlock.str", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, (await client.GetFromJsonAsync<PagedBody<NpcSummaryBody>>("/api/npcs"))!.TotalCount);
    }

    [Theory]
    [InlineData("str", 31, "Força deve estar entre 1 e 30.")]
    [InlineData("armorClass", 0, "Classe de Armadura deve estar entre 1 e 30.")]
    [InlineData("hitPoints", 0, "Pontos de vida deve ser pelo menos 1.")]
    public async Task Import_ValidationMessagesArePortuguese(string field, int value, string expected)
    {
        // No teste no navegador, a importação mostrou "The field Str must be between 1 and 30."
        var npc = Npc("Rahadin", withStatBlock: true);
        npc["statBlock"]![field] = value;

        await AssertRejectedWith(FileWith(npc).ToJsonString(), expected);
    }

    [Fact]
    public async Task Import_MissingName_HasPortugueseMessage()
    {
        var npc = Npc("x");
        npc["name"] = "";

        await AssertRejectedWith(FileWith(npc).ToJsonString(), "Nome é obrigatório.");
    }

    [Fact]
    public async Task Import_WrongFormat_Returns400WithMessage()
    {
        var file = FileWith(Npc("Um"));
        file["format"] = "outra-coisa";

        await AssertRejectedWith(file.ToJsonString(), "Este arquivo não é uma exportação de NPCs do NpcForge.");
    }

    [Fact]
    public async Task Import_NotAnExportAtAll_Returns400()
    {
        var response = await ImportRawAsync(await factory.CreateAuthenticatedClientAsync(), """{ "foo": 1 }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_NewerVersion_Returns400WithMessage()
    {
        var file = FileWith(Npc("Um"));
        file["version"] = 2;

        await AssertRejectedWith(file.ToJsonString(), "Este arquivo foi exportado por uma versão mais nova do NpcForge.");
    }

    [Fact]
    public async Task Import_EmptyList_Returns400WithMessage()
        => await AssertRejectedWith(FileWith().ToJsonString(), "O arquivo não tem NPCs.");

    [Fact]
    public async Task Import_MoreThan500_Returns400WithMessage()
        => await AssertRejectedWith(
            FileWith(Enumerable.Range(0, 501).Select(i => Npc($"NPC {i}")).ToArray()).ToJsonString(),
            "O arquivo tem mais de 500 NPCs.");

    [Fact]
    public async Task Import_UnknownFieldsAreIgnored()
    {
        var npc = Npc("Um");
        npc["mood"] = "feliz";
        var file = FileWith(npc);
        file["generator"] = "x";

        var response = await ImportAsync(await factory.CreateAuthenticatedClientAsync(), file);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Import_NormalizesHitDice()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var npc = Npc("Borin", withStatBlock: true);
        npc["statBlock"]!["hitDice"] = "4d8+4";

        await ImportAsync(client, FileWith(npc));

        var id = (await client.GetFromJsonAsync<PagedBody<NpcSummaryBody>>("/api/npcs"))!.Items[0].Id;
        Assert.Equal("4d8 + 4", (await client.GetFromJsonAsync<NpcBody>($"/api/npcs/{id}"))!.StatBlock!.HitDice);
    }

    [Fact]
    public async Task Import_WithoutToken_Returns401()
    {
        var response = await ImportAsync(factory.CreateClient(), FileWith(Npc("Um")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task AssertRejectedWith(string body, string message)
    {
        var response = await ImportRawAsync(await factory.CreateAuthenticatedClientAsync(), body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Contains(errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray()), m => m.GetString() == message);
    }

    private static JsonObject Npc(string name, bool withStatBlock = false)
    {
        var npc = new JsonObject { ["name"] = name, ["race"] = "humano", ["notes"] = "segredo" };
        npc["statBlock"] = withStatBlock ? JsonSerializer.SerializeToNode(NpcTestData.ValidStatBlock(), Web) : null;
        return npc;
    }

    private static JsonObject FileWith(params JsonObject[] npcs) => new()
    {
        ["format"] = "npcforge-npcs",
        ["version"] = 1,
        ["exportedAt"] = "2026-10-29T21:30:00Z",
        ["npcs"] = new JsonArray(npcs.Select(n => (JsonNode)n.DeepClone()).ToArray()),
    };

    private static Task<HttpResponseMessage> ImportAsync(HttpClient client, JsonObject file)
        => ImportRawAsync(client, file.ToJsonString());

    private static Task<HttpResponseMessage> ImportRawAsync(HttpClient client, string json)
        => client.PostAsync("/api/npcs/import", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
