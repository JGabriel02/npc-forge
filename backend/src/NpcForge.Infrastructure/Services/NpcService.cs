using Microsoft.EntityFrameworkCore;
using NpcForge.Core.Common;
using NpcForge.Core.Entities;
using NpcForge.Core.Npcs;
using NpcForge.Infrastructure.Persistence;

namespace NpcForge.Infrastructure.Services;

public class NpcService(AppDbContext db, TimeProvider time) : INpcService
{
    public async Task<PagedResult<NpcSummaryDto>> ListAsync(string userId, NpcQuery query, CancellationToken ct)
    {
        // Limite superior evita overflow em (page - 1) * pageSize, que viraria OFFSET negativo.
        var page = Math.Clamp(query.Page, 1, int.MaxValue / NpcQuery.MaxPageSize);
        var pageSize = Math.Clamp(query.PageSize, 1, NpcQuery.MaxPageSize);
        var npcs = NpcFiltering.Apply(Owned(userId), query);

        var total = await npcs.CountAsync(ct);
        var items = await npcs
            .OrderByDescending(n => n.UpdatedAt).ThenBy(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NpcSummaryDto(
                n.Id, n.Name, n.Race, n.Occupation, n.CampaignId,
                n.Campaign != null ? n.Campaign.Name : null,
                n.StatBlock != null,
                n.StatBlock != null ? n.StatBlock.ChallengeRating : null,
                n.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<NpcSummaryDto>(items, page, pageSize, total);
    }

    public async Task<NpcDto?> GetAsync(string userId, Guid id, CancellationToken ct)
        => (await FindAsync(userId, id, ct))?.ToDto();

    public async Task<ServiceResult<NpcDto>> CreateAsync(string userId, CreateNpcRequest request, CancellationToken ct)
    {
        if (!await IsOwnCampaignAsync(userId, request.CampaignId, ct))
            return ServiceResult<NpcDto>.Fail(ServiceError.InvalidCampaign);

        var now = time.GetUtcNow().UtcDateTime;
        var npc = new Npc
        {
            Id = Guid.NewGuid(), UserId = userId, CampaignId = request.CampaignId,
            Name = request.Name, Race = request.Race, Occupation = request.Occupation,
            Appearance = request.Appearance, Personality = request.Personality,
            Background = request.Background, Notes = request.Notes,
            CreatedAt = now, UpdatedAt = now,
        };
        npc.StatBlock = request.StatBlock?.ToEntity(npc.Id);

        db.Npcs.Add(npc);
        await db.SaveChangesAsync(ct);
        return ServiceResult<NpcDto>.Ok(npc.ToDto());
    }

    public async Task<ServiceResult<NpcDto>> UpdateAsync(
        string userId, Guid id, UpdateNpcRequest request, CancellationToken ct)
    {
        var npc = await FindAsync(userId, id, ct);
        if (npc is null)
            return ServiceResult<NpcDto>.Fail(ServiceError.NotFound);
        if (!await IsOwnCampaignAsync(userId, request.CampaignId, ct))
            return ServiceResult<NpcDto>.Fail(ServiceError.InvalidCampaign);

        npc.CampaignId = request.CampaignId;
        npc.Name = request.Name;
        npc.Race = request.Race;
        npc.Occupation = request.Occupation;
        npc.Appearance = request.Appearance;
        npc.Personality = request.Personality;
        npc.Background = request.Background;
        npc.Notes = request.Notes;
        npc.UpdatedAt = time.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
        return ServiceResult<NpcDto>.Ok(npc.ToDto());
    }

    public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct)
        => await Owned(userId).Where(n => n.Id == id).ExecuteDeleteAsync(ct) > 0;

    public async Task<NpcDto?> SetStatBlockAsync(
        string userId, Guid npcId, StatBlockRequest request, CancellationToken ct)
    {
        var npc = await FindAsync(userId, npcId, ct);
        if (npc is null) return null;

        if (npc.StatBlock is not null)
            db.StatBlocks.Remove(npc.StatBlock);
        npc.StatBlock = request.ToEntity(npc.Id);
        npc.UpdatedAt = time.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(ct);
        return npc.ToDto();
    }

    public async Task<NpcDto?> RemoveStatBlockAsync(string userId, Guid npcId, CancellationToken ct)
    {
        var npc = await FindAsync(userId, npcId, ct);
        if (npc is null) return null;

        if (npc.StatBlock is not null)
        {
            db.StatBlocks.Remove(npc.StatBlock);
            npc.StatBlock = null;
            npc.UpdatedAt = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
        return npc.ToDto();
    }

    private IQueryable<Npc> Owned(string userId) => db.Npcs.Where(n => n.UserId == userId);

    private Task<Npc?> FindAsync(string userId, Guid id, CancellationToken ct)
        => Owned(userId).Include(n => n.StatBlock).SingleOrDefaultAsync(n => n.Id == id, ct);

    private async Task<bool> IsOwnCampaignAsync(string userId, Guid? campaignId, CancellationToken ct)
        => campaignId is null || await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.UserId == userId, ct);
}
