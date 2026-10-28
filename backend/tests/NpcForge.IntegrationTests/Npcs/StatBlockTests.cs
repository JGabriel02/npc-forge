using System.Net;
using System.Net.Http.Json;

namespace NpcForge.IntegrationTests.Npcs;

[Collection("api")]
public class StatBlockTests(ApiFactory factory)
{
    [Fact]
    public async Task Put_AddsStatBlockToNarrativeNpc()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var npc = await client.CreateNpcAsync("Hilda");

        var response = await client.PutAsJsonAsync($"/api/npcs/{npc.Id}/statblock", NpcTestData.ValidStatBlock());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<NpcBody>();
        Assert.NotNull(updated!.StatBlock);
        Assert.Equal("Hilda", updated.Name);
        Assert.True(updated.UpdatedAt > npc.UpdatedAt);
    }

    [Fact]
    public async Task Put_ReplacesExistingStatBlock()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var npc = await client.CreateNpcAsync(statBlock: NpcTestData.ValidStatBlock());
        var replacement = NpcTestData.ValidStatBlock() with
        {
            ChallengeRating = "10",
            Actions = [new ActionPayload("Machado Rúnico", "Ataque mágico.", 9, "2d12+5")],
            Traits = [],
        };

        var response = await client.PutAsJsonAsync($"/api/npcs/{npc.Id}/statblock", replacement);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, npc.StatBlock!.ProficiencyBonus);

        // Relê do banco para garantir que a substituição foi persistida, não só montada em memória.
        var reloaded = await client.GetFromJsonAsync<NpcBody>($"/api/npcs/{npc.Id}");
        Assert.Equal("10", reloaded!.StatBlock!.ChallengeRating);
        Assert.Equal(4, reloaded.StatBlock.ProficiencyBonus);
        Assert.Equal(["Machado Rúnico"], reloaded.StatBlock.Actions.Select(a => a.Name));
        Assert.Equal(9, reloaded.StatBlock.Actions[0].AttackBonus);
        Assert.Empty(reloaded.StatBlock.Traits);
    }

    [Fact]
    public async Task Put_InvalidStatBlock_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var npc = await client.CreateNpcAsync();

        var response = await client.PutAsJsonAsync($"/api/npcs/{npc.Id}/statblock", NpcTestData.ValidStatBlock() with { Dex = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_NullListElement_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var npc = await client.CreateNpcAsync();
        var body = System.Text.Json.Nodes.JsonNode.Parse(
            System.Text.Json.JsonSerializer.Serialize(NpcTestData.ValidStatBlock(), JsonOptions))!;
        body["traits"] = System.Text.Json.Nodes.JsonNode.Parse("[null]");

        var response = await client.PutAsJsonAsync($"/api/npcs/{npc.Id}/statblock", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(30, HttpStatusCode.OK)]
    [InlineData(31, HttpStatusCode.BadRequest)]
    public async Task Put_TraitsCount_LimitedTo30(int count, HttpStatusCode expected)
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var npc = await client.CreateNpcAsync();
        var statBlock = NpcTestData.ValidStatBlock() with
        {
            Traits = Enumerable.Range(1, count).Select(i => new FeaturePayload($"Traço {i}", "...")).ToList(),
        };

        var response = await client.PutAsJsonAsync($"/api/npcs/{npc.Id}/statblock", statBlock);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("99")]
    [InlineData("\"Enorme\"")]
    [InlineData(null)]
    public async Task Put_InvalidOrMissingSize_Returns400(string? sizeJson)
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var npc = await client.CreateNpcAsync();
        var body = System.Text.Json.Nodes.JsonNode.Parse(
            System.Text.Json.JsonSerializer.Serialize(NpcTestData.ValidStatBlock(), JsonOptions))!.AsObject();
        body.Remove("size");
        if (sizeJson is not null) body["size"] = System.Text.Json.Nodes.JsonNode.Parse(sizeJson);

        var response = await client.PutAsJsonAsync($"/api/npcs/{npc.Id}/statblock", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    [Fact]
    public async Task Delete_RemovesStatBlock_NpcRemainsNarrative()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var npc = await client.CreateNpcAsync(statBlock: NpcTestData.ValidStatBlock());

        var response = await client.DeleteAsync($"/api/npcs/{npc.Id}/statblock");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<NpcBody>();
        Assert.Null(updated!.StatBlock);
        var narrative = await client.GetFromJsonAsync<PagedBody<NpcSummaryBody>>("/api/npcs?hasStatBlock=false");
        Assert.Contains(narrative!.Items, n => n.Id == npc.Id);
    }

    [Fact]
    public async Task OtherUser_PutDelete_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var intruder = await factory.CreateAuthenticatedClientAsync();
        var npc = await owner.CreateNpcAsync(statBlock: NpcTestData.ValidStatBlock());
        var url = $"/api/npcs/{npc.Id}/statblock";

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutAsJsonAsync(url, NpcTestData.ValidStatBlock())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync(url)).StatusCode);
        var stillThere = await owner.GetFromJsonAsync<NpcBody>($"/api/npcs/{npc.Id}");
        Assert.NotNull(stillThere!.StatBlock);
    }
}
