using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NpcForge.Infrastructure.Demo;
using NpcForge.Infrastructure.Identity;
using NpcForge.Infrastructure.Persistence;

namespace NpcForge.IntegrationTests.Demo;

// O banco da coleção "api" é compartilhado: a conta demo pode já existir quando cada teste roda.
[Collection("api")]
public class DemoSeederTests(ApiFactory factory)
{
    [Fact]
    public async Task EnsureSeeded_TwiceInARow_CreatesOneAccountAndContentOnce()
    {
        var first = await DemoSeeding.SeedAsync(factory);
        var second = await DemoSeeding.SeedAsync(factory);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(8, await DemoSeeding.CountNpcsAsync(factory, first.Id));
        Assert.Equal(2, await DemoSeeding.CountCampaignsAsync(factory, first.Id));
    }

    [Fact]
    public async Task SeedContent_LinksCampaignsAsInFile()
    {
        var demo = await DemoSeeding.SeedAsync(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var npcs = await db.Npcs.Where(n => n.UserId == demo.Id).Include(n => n.Campaign).Include(n => n.StatBlock).ToListAsync();
        Assert.Equal(3, npcs.Count(n => n.Campaign?.Name == "A Estalagem do Corvo Branco"));
        Assert.Equal(3, npcs.Count(n => n.Campaign?.Name == "Contrabandistas de Porto Névoa"));
        Assert.Equal(2, npcs.Count(n => n.CampaignId is null));
        Assert.Equal(4, npcs.Count(n => n.StatBlock is not null));
    }

    [Fact]
    public async Task SeedFile_IsAValidExportFile()
    {
        // Valida pela rota real: um usuário comum importa o arquivo da demo.
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/api/npcs/import",
            new StringContent(DemoSeeder.ReadSeedFile(), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task DemoAccount_CannotLogInWithAPassword()
    {
        await DemoSeeding.SeedAsync(factory);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = DemoSeeder.Email, password = TestAuthExtensions.TestPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

[Collection("api-demo-seed")]
public class DemoSeederConcurrencyTests(DemoSeedApiFactory factory)
{
    [Fact]
    public async Task EnsureSeeded_Concurrently_CreatesOneAccount()
    {
        // Dois visitantes clicando ao mesmo tempo no primeiro uso.
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => DemoSeeding.SeedAsync(factory)));

        Assert.Single(results.Select(u => u.Id).Distinct());
        Assert.Equal(8, await DemoSeeding.CountNpcsAsync(factory, results[0].Id));
        Assert.Equal(2, await DemoSeeding.CountCampaignsAsync(factory, results[0].Id));
    }
}

internal static class DemoSeeding
{
    // Cada chamada num scope próprio, como cada requisição na API.
    public static async Task<AppUser> SeedAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DemoSeeder>().EnsureSeededAsync(CancellationToken.None);
    }

    public static async Task<int> CountNpcsAsync(ApiFactory factory, string userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Npcs.CountAsync(n => n.UserId == userId);
    }

    public static async Task<int> CountCampaignsAsync(ApiFactory factory, string userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Campaigns.CountAsync(c => c.UserId == userId);
    }
}
