using System.Net.Http.Json;

namespace NpcForge.IntegrationTests.Npcs;

[Collection("api")]
public class NpcsListTests(ApiFactory factory)
{
    [Fact]
    public async Task FiltersByCampaignId()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var campaign = await client.CreateCampaignAsync();
        await client.CreateNpcAsync("Dentro", campaign);
        await client.CreateNpcAsync("Fora");

        var page = await ListAsync(client, $"campaignId={campaign}");

        Assert.Equal(["Dentro"], page.Items.Select(n => n.Name));
        Assert.Equal("Campanha", page.Items[0].CampaignName);
    }

    [Fact]
    public async Task FiltersByCampaignNone()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var campaign = await client.CreateCampaignAsync();
        await client.CreateNpcAsync("Dentro", campaign);
        await client.CreateNpcAsync("Fora");

        var page = await ListAsync(client, "campaignId=none");

        Assert.Equal(["Fora"], page.Items.Select(n => n.Name));
    }

    [Theory]
    [InlineData(true, "Inimigo")]
    [InlineData(false, "Narrativo")]
    public async Task FiltersByHasStatBlock(bool hasStatBlock, string expected)
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await client.CreateNpcAsync("Inimigo", statBlock: NpcTestData.ValidStatBlock());
        await client.CreateNpcAsync("Narrativo");

        var page = await ListAsync(client, $"hasStatBlock={hasStatBlock}");

        Assert.Equal([expected], page.Items.Select(n => n.Name));
        Assert.Equal(hasStatBlock, page.Items[0].HasStatBlock);
        Assert.Equal(hasStatBlock ? "1" : null, page.Items[0].ChallengeRating);
    }

    [Fact]
    public async Task Search_MatchesNameRaceOrOccupation_CaseInsensitiveWithAccentedLetters()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await client.CreateNpcAsync("Borin", race: "anão");
        await client.CreateNpcAsync("Hilda", occupation: "Taverneira");
        await client.CreateNpcAsync("Tharivol");

        Assert.Equal(["Borin"], (await ListAsync(client, "search=ANÃO")).Items.Select(n => n.Name));
        Assert.Equal(["Hilda"], (await ListAsync(client, "search=tavern")).Items.Select(n => n.Name));
        Assert.Equal(["Tharivol"], (await ListAsync(client, "search=hariv")).Items.Select(n => n.Name));
    }

    [Fact]
    public async Task Search_WildcardCharactersAreLiteral()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await client.CreateNpcAsync("Bob");
        await client.CreateNpcAsync("100% Bob");
        await client.CreateNpcAsync("Bob_2");

        Assert.Equal(["100% Bob"], (await ListAsync(client, "search=%25")).Items.Select(n => n.Name));
        Assert.Equal(["Bob_2"], (await ListAsync(client, "search=_")).Items.Select(n => n.Name));
    }

    [Fact]
    public async Task Paging_ClampsInvalidValues()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await client.CreateNpcAsync("Um");
        await client.CreateNpcAsync("Dois");

        var huge = await ListAsync(client, "pageSize=1000");
        var invalid = await ListAsync(client, "page=-1&pageSize=0");

        Assert.Equal(100, huge.PageSize);
        Assert.Equal(1, invalid.Page);
        Assert.Equal(1, invalid.PageSize);
        Assert.Single(invalid.Items);
    }

    [Fact]
    public async Task Paging_ReturnsTotalCountAndRequestedPage_MostRecentlyUpdatedFirst()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await client.CreateNpcAsync("Primeiro");
        await client.CreateNpcAsync("Segundo");
        await client.CreateNpcAsync("Terceiro");

        var first = await ListAsync(client, "pageSize=2&page=1");
        var second = await ListAsync(client, "pageSize=2&page=2");

        Assert.Equal(["Terceiro", "Segundo"], first.Items.Select(n => n.Name));
        Assert.Equal(["Primeiro"], second.Items.Select(n => n.Name));
        Assert.Equal(3, second.TotalCount);
    }

    [Fact]
    public async Task List_ExcludesOtherUsersNpcs()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var other = await factory.CreateAuthenticatedClientAsync();
        await client.CreateNpcAsync("Meu");
        await other.CreateNpcAsync("Alheio");

        var page = await ListAsync(client, "");

        Assert.Equal(["Meu"], page.Items.Select(n => n.Name));
        Assert.Equal(1, page.TotalCount);
    }

    private static async Task<PagedBody<NpcSummaryBody>> ListAsync(HttpClient client, string query)
        => (await client.GetFromJsonAsync<PagedBody<NpcSummaryBody>>($"/api/npcs?{query}"))!;
}
