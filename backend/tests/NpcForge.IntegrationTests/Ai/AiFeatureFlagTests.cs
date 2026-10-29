using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NpcForge.IntegrationTests.Ai;

[Collection("api")]
public class AiEnabledFeatureTests(ApiFactory factory)
{
    [Fact]
    public async Task Features_ReportsAiEnabled_WithoutLogin()
    {
        var features = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/features");

        Assert.True(features.GetProperty("ai").GetBoolean());
    }
}

[Collection("api-ai-disabled")]
public class AiDisabledFeatureTests(AiDisabledApiFactory factory)
{
    [Fact]
    public async Task Features_ReportsAiDisabled()
    {
        var features = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/features");

        Assert.False(features.GetProperty("ai").GetBoolean());
    }

    [Fact]
    public async Task GenerateNpc_WhenDisabled_Returns404InPortuguese()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/ai/generate-npc", new { prompt = "x", withStatBlock = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("A IA está desativada nesta instalação.", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Usage_WhenDisabled_Returns404()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/ai/usage")).StatusCode);
    }

    [Fact]
    public async Task CrudStillWorks_WhenAiDisabled()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var campaign = await client.PostAsJsonAsync("/api/campaigns", new { name = "Mesa sem IA" });
        var campaignId = (await campaign.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var npc = await client.PostAsJsonAsync("/api/npcs", new { name = "Hilda", campaignId });

        Assert.Equal(HttpStatusCode.Created, campaign.StatusCode);
        Assert.Equal(HttpStatusCode.Created, npc.StatusCode);
    }
}
