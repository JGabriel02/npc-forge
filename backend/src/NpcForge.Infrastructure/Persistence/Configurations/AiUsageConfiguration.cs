using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpcForge.Core.Entities;
using NpcForge.Infrastructure.Identity;

namespace NpcForge.Infrastructure.Persistence.Configurations;

public class AiUsageConfiguration : IEntityTypeConfiguration<AiUsage>
{
    public void Configure(EntityTypeBuilder<AiUsage> builder)
    {
        // Uma linha por usuário por dia: é a chave que o ON CONFLICT do AiUsageService usa.
        builder.HasKey(u => new { u.UserId, u.Date });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
