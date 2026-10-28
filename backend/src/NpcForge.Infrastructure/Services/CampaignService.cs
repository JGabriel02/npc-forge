using Microsoft.EntityFrameworkCore;
using NpcForge.Core.Campaigns;
using NpcForge.Core.Entities;
using NpcForge.Infrastructure.Persistence;

namespace NpcForge.Infrastructure.Services;

public class CampaignService(AppDbContext db, TimeProvider time) : ICampaignService
{
    public async Task<IReadOnlyList<CampaignDto>> ListAsync(string userId, CancellationToken ct)
        => await Owned(userId)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(ToDto)
            .ToListAsync(ct);

    public Task<CampaignDto?> GetAsync(string userId, Guid id, CancellationToken ct)
        => Owned(userId).Where(c => c.Id == id).Select(ToDto).SingleOrDefaultAsync(ct);

    public async Task<CampaignDto> CreateAsync(string userId, CampaignRequest request, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var campaign = new Campaign { Id = Guid.NewGuid(), UserId = userId, CreatedAt = now, UpdatedAt = now };
        Apply(campaign, request);

        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(userId, campaign.Id, ct))!;
    }

    public async Task<CampaignDto?> UpdateAsync(string userId, Guid id, CampaignRequest request, CancellationToken ct)
    {
        var campaign = await Owned(userId).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return null;

        Apply(campaign, request);
        campaign.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, id, ct);
    }

    public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct)
        => await Owned(userId).Where(c => c.Id == id).ExecuteDeleteAsync(ct) > 0;

    private IQueryable<Campaign> Owned(string userId) => db.Campaigns.Where(c => c.UserId == userId);

    private static void Apply(Campaign campaign, CampaignRequest request)
    {
        campaign.Name = request.Name;
        campaign.Description = request.Description;
        campaign.Setting = request.Setting;
    }

    private static readonly System.Linq.Expressions.Expression<Func<Campaign, CampaignDto>> ToDto = c =>
        new CampaignDto(c.Id, c.Name, c.Description, c.Setting, c.Npcs.Count, c.CreatedAt, c.UpdatedAt);
}
