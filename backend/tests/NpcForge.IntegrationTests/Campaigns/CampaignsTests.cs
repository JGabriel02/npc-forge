using System.Net;
using System.Net.Http.Json;

namespace NpcForge.IntegrationTests.Campaigns;

[Collection("api")]
public class CampaignsTests(ApiFactory factory)
{
    [Fact]
    public async Task Create_ThenGet_ReturnsCampaignWithZeroNpcs()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync("/api/campaigns",
            new { name = "Maldição de Strahd", description = "Campanha de terror", setting = "terror gótico, Barovia" });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var created = await create.Content.ReadFromJsonAsync<CampaignBody>();

        var get = await client.GetFromJsonAsync<CampaignBody>($"/api/campaigns/{created!.Id}");
        Assert.Equal("Maldição de Strahd", get!.Name);
        Assert.Equal("terror gótico, Barovia", get.Setting);
        Assert.Equal(0, get.NpcCount);
    }

    [Fact]
    public async Task Update_ChangesFieldsAndUpdatedAt()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var created = await CreateAsync(client, "Antigo");

        var put = await client.PutAsJsonAsync($"/api/campaigns/{created.Id}", new { name = "Novo", setting = "fantasia" });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await put.Content.ReadFromJsonAsync<CampaignBody>();
        Assert.Equal("Novo", updated!.Name);
        Assert.Equal("fantasia", updated.Setting);
        Assert.True(updated.UpdatedAt > created.UpdatedAt);
    }

    [Fact]
    public async Task Delete_Returns204_ThenGetReturns404()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var created = await CreateAsync(client, "Temporária");

        var delete = await client.DeleteAsync($"/api/campaigns/{created.Id}");
        var get = await client.GetAsync($"/api/campaigns/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnCampaigns_MostRecentlyUpdatedFirst()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var other = await factory.CreateAuthenticatedClientAsync();
        var first = await CreateAsync(client, "Primeira");
        await CreateAsync(client, "Segunda");
        await CreateAsync(other, "De outra pessoa");
        await client.PutAsJsonAsync($"/api/campaigns/{first.Id}", new { name = "Primeira editada" });

        var list = await client.GetFromJsonAsync<List<CampaignBody>>("/api/campaigns");

        Assert.Equal(["Primeira editada", "Segunda"], list!.Select(c => c.Name));
    }

    [Fact]
    public async Task OtherUser_GetPutDelete_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var intruder = await factory.CreateAuthenticatedClientAsync();
        var created = await CreateAsync(owner, "Secreta");
        var url = $"/api/campaigns/{created.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutAsJsonAsync(url, new { name = "Hack" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Create_InvalidNameLength_Returns400(int length)
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/campaigns", new { name = new string('a', length) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/campaigns");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<CampaignBody> CreateAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/campaigns", new { name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CampaignBody>())!;
    }
}

public record CampaignBody(
    Guid Id, string Name, string? Description, string? Setting, int NpcCount, DateTime CreatedAt, DateTime UpdatedAt);
