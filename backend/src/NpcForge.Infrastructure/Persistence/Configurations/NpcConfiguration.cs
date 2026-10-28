using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpcForge.Core;
using NpcForge.Core.Entities;
using NpcForge.Infrastructure.Identity;

namespace NpcForge.Infrastructure.Persistence.Configurations;

public class NpcConfiguration : IEntityTypeConfiguration<Npc>
{
    public void Configure(EntityTypeBuilder<Npc> builder)
    {
        builder.Property(n => n.Name).HasMaxLength(NpcLimits.ShortText).IsRequired();
        builder.Property(n => n.Race).HasMaxLength(NpcLimits.ShortText);
        builder.Property(n => n.Occupation).HasMaxLength(NpcLimits.ShortText);
        builder.Property(n => n.Appearance).HasMaxLength(NpcLimits.LongText);
        builder.Property(n => n.Personality).HasMaxLength(NpcLimits.LongText);
        builder.Property(n => n.Background).HasMaxLength(NpcLimits.VeryLongText);
        builder.Property(n => n.Notes).HasMaxLength(NpcLimits.VeryLongText);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);

        // Excluir a campanha não apaga os NPCs: eles ficam "Sem campanha".
        builder.HasOne(n => n.Campaign).WithMany(c => c.Npcs)
            .HasForeignKey(n => n.CampaignId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(n => n.StatBlock).WithOne()
            .HasForeignKey<StatBlock>(s => s.NpcId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.UserId, n.UpdatedAt });
    }
}
