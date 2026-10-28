using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NpcForge.Core.Entities;
using NpcForge.Infrastructure.Identity;
using NpcForge.Infrastructure.Persistence;

namespace NpcForge.IntegrationTests.Persistence;

[Collection("api")]
public class AppDbContextTests(ApiFactory factory)
{
    [Fact]
    public async Task SavesAndLoadsNpcWithStatBlockAndJsonLists()
    {
        var userId = await CreateUserAsync();
        var npc = NewNpc(userId);
        npc.StatBlock = new StatBlock
        {
            Size = CreatureSize.Medium, CreatureType = "humanoid (dwarf)", Alignment = "lawful neutral",
            ArmorClass = 16, HitPoints = 26, HitDice = "4d8+8", Speed = "25 ft.",
            Str = 16, Dex = 10, Con = 14, Int = 10, Wis = 12, Cha = 8, ChallengeRating = "1",
            Traits = [new StatBlockFeature { Name = "Resiliência Anã", Description = "Vantagem contra veneno." }],
            Actions =
            [
                new StatBlockAction { Name = "Martelo", Description = "Ataque corpo a corpo.", AttackBonus = 5, Damage = "1d8+3" },
                new StatBlockAction { Name = "Grito", Description = "Assusta inimigos." },
            ],
        };
        await SaveAsync(npc);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var loaded = await db.Npcs.Include(n => n.StatBlock).SingleAsync(n => n.Id == npc.Id);

        Assert.NotNull(loaded.StatBlock);
        Assert.Equal(2, loaded.StatBlock.Actions.Count);
        Assert.Equal(5, loaded.StatBlock.Actions[0].AttackBonus);
        Assert.Equal("1d8+3", loaded.StatBlock.Actions[0].Damage);
        Assert.Single(loaded.StatBlock.Traits);
        Assert.Equal(CreatureSize.Medium, loaded.StatBlock.Size);
    }

    [Fact]
    public async Task DeletingNpcDeletesStatBlock()
    {
        var npc = NewNpc(await CreateUserAsync());
        npc.StatBlock = new StatBlock { HitDice = "1d8", ChallengeRating = "0", HitPoints = 4, ArmorClass = 10 };
        await SaveAsync(npc);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Npcs.Where(n => n.Id == npc.Id).ExecuteDeleteAsync();

        Assert.Equal(0, await db.StatBlocks.CountAsync(s => s.NpcId == npc.Id));
    }

    [Fact]
    public async Task DeletingCampaignKeepsNpcsWithoutCampaign()
    {
        var userId = await CreateUserAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), UserId = userId, Name = "Mesa de sexta" };
        var npc = NewNpc(userId);
        npc.Campaign = campaign;
        await SaveAsync(npc);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Campaigns.Where(c => c.Id == campaign.Id).ExecuteDeleteAsync();

        var loaded = await db.Npcs.AsNoTracking().SingleOrDefaultAsync(n => n.Id == npc.Id);
        Assert.NotNull(loaded);
        Assert.Null(loaded.CampaignId);
    }

    private static Npc NewNpc(string userId) => new() { Id = Guid.NewGuid(), UserId = userId, Name = "Borin" };

    private async Task<string> CreateUserAsync()
    {
        var user = new AppUser { UserName = $"{Guid.NewGuid()}@test.local", DisplayName = "Teste" };
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task SaveAsync(Npc npc)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Npcs.Add(npc);
        await db.SaveChangesAsync();
    }
}
