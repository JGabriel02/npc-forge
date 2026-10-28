using System.ComponentModel.DataAnnotations;

namespace NpcForge.Core.Campaigns;

public record CampaignRequest(
    [Required, MaxLength(NpcLimits.ShortText)] string Name,
    [MaxLength(NpcLimits.LongText)] string? Description,
    [MaxLength(NpcLimits.LongText)] string? Setting);

public record CampaignDto(
    Guid Id, string Name, string? Description, string? Setting, int NpcCount, DateTime CreatedAt, DateTime UpdatedAt);
