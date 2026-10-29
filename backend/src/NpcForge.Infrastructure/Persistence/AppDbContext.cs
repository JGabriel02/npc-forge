using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NpcForge.Core.Entities;
using NpcForge.Infrastructure.Identity;

namespace NpcForge.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Npc> Npcs => Set<Npc>();
    public DbSet<StatBlock> StatBlocks => Set<StatBlock>();
    public DbSet<AiUsage> AiUsages => Set<AiUsage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        builder.Entity<AppUser>().Property(u => u.DisplayName).HasMaxLength(50);
    }
}
