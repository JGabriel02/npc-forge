using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpcForge.Core;
using NpcForge.Core.Entities;

namespace NpcForge.Infrastructure.Persistence.Configurations;

public class StatBlockConfiguration : IEntityTypeConfiguration<StatBlock>
{
    public void Configure(EntityTypeBuilder<StatBlock> builder)
    {
        builder.HasKey(s => s.NpcId);

        builder.Property(s => s.Size).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.CreatureType).HasMaxLength(NpcLimits.ShortText);
        builder.Property(s => s.Alignment).HasMaxLength(NpcLimits.ShortText);
        builder.Property(s => s.HitDice).HasMaxLength(NpcLimits.ShortText);
        builder.Property(s => s.Speed).HasMaxLength(NpcLimits.ShortText);
        builder.Property(s => s.ChallengeRating).HasMaxLength(10);
        builder.Property(s => s.SavingThrows).HasMaxLength(NpcLimits.MediumText);
        builder.Property(s => s.Skills).HasMaxLength(NpcLimits.MediumText);
        builder.Property(s => s.Senses).HasMaxLength(NpcLimits.MediumText);
        builder.Property(s => s.Languages).HasMaxLength(NpcLimits.MediumText);

        // Listas sempre lidas junto com a ficha: colunas jsonb em vez de tabelas separadas.
        builder.OwnsMany(s => s.Traits, b => b.ToJson());
        builder.OwnsMany(s => s.Actions, b => b.ToJson());
        builder.OwnsMany(s => s.Reactions, b => b.ToJson());
    }
}
