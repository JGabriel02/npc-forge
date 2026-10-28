using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpcForge.Core;
using NpcForge.Core.Entities;
using NpcForge.Infrastructure.Identity;

namespace NpcForge.Infrastructure.Persistence.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(NpcLimits.ShortText).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(NpcLimits.LongText);
        builder.Property(c => c.Setting).HasMaxLength(NpcLimits.LongText);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => c.UserId);
    }
}
