namespace NpcForge.Core.Campaigns;

/// <summary>CRUD de campanhas, sempre restrito ao usuário informado. <c>null</c>/<c>false</c> = não encontrada.</summary>
public interface ICampaignService
{
    Task<IReadOnlyList<CampaignDto>> ListAsync(string userId, CancellationToken ct);
    Task<CampaignDto?> GetAsync(string userId, Guid id, CancellationToken ct);
    Task<CampaignDto> CreateAsync(string userId, CampaignRequest request, CancellationToken ct);
    Task<CampaignDto?> UpdateAsync(string userId, Guid id, CampaignRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct);
}
