using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using NpcForge.IntegrationTests.Campaigns;

namespace NpcForge.IntegrationTests.Npcs;

[Collection("api")]
public class NpcsCrudTests(ApiFactory factory)
{
    [Fact]
    public async Task Create_NarrativeNpc_Returns201AndGetReturnsItWithoutStatBlock()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/npcs", new
        {
            name = "Hilda", race = "humana", occupation = "taverneira",
            personality = "simpática", background = "viúva", notes = "sabe do culto",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<NpcBody>();
        var get = await client.GetFromJsonAsync<NpcBody>($"/api/npcs/{created!.Id}");
        Assert.Equal("Hilda", get!.Name);
        Assert.Equal("taverneira", get.Occupation);
        Assert.Equal("sabe do culto", get.Notes);
        Assert.Null(get.StatBlock);
        Assert.Null(get.CampaignId);
    }

    [Fact]
    public async Task Create_WithStatBlock_ReturnsDerivedValues()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var statBlock = NpcTestData.ValidStatBlock() with { Str = 18, ChallengeRating = "5" };

        var npc = await client.CreateNpcAsync(statBlock: statBlock);

        Assert.NotNull(npc.StatBlock);
        Assert.Equal(4, npc.StatBlock.Modifiers.Str);
        Assert.Equal(0, npc.StatBlock.Modifiers.Dex);
        Assert.Equal(3, npc.StatBlock.ProficiencyBonus);
        Assert.Equal(5, npc.StatBlock.Actions[0].AttackBonus);
        Assert.Equal("1d8+3", npc.StatBlock.Actions[0].Damage);
        Assert.Equal("Medium", npc.StatBlock.Size);
    }

    [Theory]
    [InlineData("2d8 + 2", "2d8 + 2")]
    [InlineData("1d4 – 1", "1d4 – 1")]
    [InlineData("4d8+8", "4d8 + 8")]
    public async Task Create_HitDiceInBookFormat_IsAcceptedAndStoredNormalized(string sent, string stored)
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var npc = await client.CreateNpcAsync(statBlock: NpcTestData.ValidStatBlock() with { HitDice = sent });

        var reloaded = await client.GetFromJsonAsync<NpcBody>($"/api/npcs/{npc.Id}");
        Assert.Equal(stored, reloaded!.StatBlock!.HitDice);
    }

    public static TheoryData<string, StatBlockPayload> InvalidStatBlocks => new()
    {
        { "Str", NpcTestData.ValidStatBlock() with { Str = 31 } },
        { "ChallengeRating", NpcTestData.ValidStatBlock() with { ChallengeRating = "1/3" } },
        { "HitDice", NpcTestData.ValidStatBlock() with { HitDice = "4d" } },
        { "HitPoints", NpcTestData.ValidStatBlock() with { HitPoints = 0 } },
    };

    [Theory]
    [MemberData(nameof(InvalidStatBlocks))]
    public async Task Create_InvalidStatBlock_Returns400(string field, StatBlockPayload statBlock)
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/npcs", new { name = "Inválido", statBlock });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => k.EndsWith(field, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_WithOtherUsersCampaign_Returns400WithCampaignIdError()
    {
        var other = await factory.CreateAuthenticatedClientAsync();
        var foreignCampaign = await other.CreateCampaignAsync();
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/npcs", new { name = "Espião", campaignId = foreignCampaign });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problem!.Errors.Keys, k => k.Equals("campaignId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Update_MovesNpcToAnotherCampaign_AndKeepsStatBlock()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var from = await client.CreateCampaignAsync("A");
        var to = await client.CreateCampaignAsync("B");
        var npc = await client.CreateNpcAsync(campaignId: from, statBlock: NpcTestData.ValidStatBlock());

        var response = await client.PutAsJsonAsync($"/api/npcs/{npc.Id}", new { name = "Borin, o Velho", campaignId = to });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<NpcBody>();
        Assert.Equal(to, updated!.CampaignId);
        Assert.Equal("Borin, o Velho", updated.Name);
        Assert.NotNull(updated.StatBlock);
        Assert.True(updated.UpdatedAt > npc.UpdatedAt);
    }

    [Fact]
    public async Task Delete_Returns204_ThenGetReturns404()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var npc = await client.CreateNpcAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/npcs/{npc.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/npcs/{npc.Id}")).StatusCode);
    }

    [Fact]
    public async Task OtherUser_GetPutDelete_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var intruder = await factory.CreateAuthenticatedClientAsync();
        var npc = await owner.CreateNpcAsync();
        var url = $"/api/npcs/{npc.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutAsJsonAsync(url, new { name = "Hack" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task DeletingCampaign_KeepsNpcWithNullCampaignId()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var campaign = await client.CreateCampaignAsync();
        var npc = await client.CreateNpcAsync(campaignId: campaign);

        await client.DeleteAsync($"/api/campaigns/{campaign}");

        var get = await client.GetFromJsonAsync<NpcBody>($"/api/npcs/{npc.Id}");
        Assert.Null(get!.CampaignId);
    }

    [Fact]
    public async Task CampaignNpcCount_ReflectsNpcs()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var campaign = await client.CreateCampaignAsync();
        await client.CreateNpcAsync("Um", campaign);
        await client.CreateNpcAsync("Dois", campaign);

        var body = await client.GetFromJsonAsync<CampaignBody>($"/api/campaigns/{campaign}");

        Assert.Equal(2, body!.NpcCount);
    }
}
