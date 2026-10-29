using Microsoft.Extensions.DependencyInjection;
using NpcForge.Core.Ai;
using NpcForge.Core.Entities;
using NpcForge.Infrastructure.Identity;
using NpcForge.Infrastructure.Persistence;
using NpcForge.Infrastructure.Services;

namespace NpcForge.IntegrationTests.Ai;

[Collection("api")]
public class AiUsageServiceTests(ApiFactory factory)
{
    private const int Limit = 5;

    [Fact]
    public async Task TryReserve_AllowsUpToLimit_ThenRefuses()
    {
        var userId = await CreateUserAsync();

        var results = new List<bool>();
        for (var i = 0; i < Limit + 1; i++)
            results.Add(await WithService(s => s.TryReserveAsync(userId, CancellationToken.None)));

        Assert.Equal([true, true, true, true, true, false], results);
        var usage = await WithService(s => s.GetAsync(userId, CancellationToken.None));
        Assert.Equal(Limit, usage.Used);
        Assert.Equal(Limit, usage.Limit);
        Assert.True(usage.ResetsAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Release_GivesBackOneReservation()
    {
        var userId = await CreateUserAsync();
        await WithService(s => s.TryReserveAsync(userId, CancellationToken.None));
        await WithService(s => s.TryReserveAsync(userId, CancellationToken.None));

        await WithService(async s => { await s.ReleaseAsync(userId, CancellationToken.None); return 0; });

        Assert.Equal(1, (await WithService(s => s.GetAsync(userId, CancellationToken.None))).Used);
    }

    [Fact]
    public async Task Release_AtZero_StaysAtZero()
    {
        var userId = await CreateUserAsync();

        await WithService(async s => { await s.ReleaseAsync(userId, CancellationToken.None); return 0; });

        Assert.Equal(0, (await WithService(s => s.GetAsync(userId, CancellationToken.None))).Used);
    }

    [Fact]
    public async Task Reservations_AreCountedPerUtcDay()
    {
        var userId = await CreateUserAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
            db.AiUsages.Add(new AiUsage { UserId = userId, Date = yesterday, GenerationCount = Limit });
            await db.SaveChangesAsync();
        }

        Assert.True(await WithService(s => s.TryReserveAsync(userId, CancellationToken.None)));
        Assert.Equal(1, (await WithService(s => s.GetAsync(userId, CancellationToken.None))).Used);
    }

    [Fact]
    public async Task ConcurrentReservations_NeverExceedLimit()
    {
        var userId = await CreateUserAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => WithService(s => s.TryReserveAsync(userId, CancellationToken.None))));

        Assert.Equal(Limit, results.Count(r => r));
        Assert.Equal(Limit, (await WithService(s => s.GetAsync(userId, CancellationToken.None))).Used);
    }

    [Fact]
    public async Task LimitZero_MeansUnlimited_ButStillCounts()
    {
        var userId = await CreateUserAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unlimited = new AiUsageService(db, TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(new AiOptions { DailyGenerationLimit = 0 }));

        var results = new List<bool>();
        for (var i = 0; i < 8; i++)
            results.Add(await unlimited.TryReserveAsync(userId, CancellationToken.None));

        Assert.All(results, Assert.True);
        var usage = await unlimited.GetAsync(userId, CancellationToken.None);
        Assert.Equal(8, usage.Used);
        Assert.Equal(0, usage.Limit);
    }

    // Cada chamada usa seu próprio escopo (e DbContext), como requisições HTTP diferentes.
    private async Task<T> WithService<T>(Func<IAiUsageService, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IAiUsageService>());
    }

    private async Task<string> CreateUserAsync()
    {
        var user = new AppUser { UserName = $"{Guid.NewGuid()}@test.local", DisplayName = "Teste" };
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
}
