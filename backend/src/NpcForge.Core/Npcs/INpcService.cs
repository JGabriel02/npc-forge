using NpcForge.Core.Common;

namespace NpcForge.Core.Npcs;

/// <summary>CRUD de NPCs, sempre restrito ao usuário informado.</summary>
public interface INpcService
{
    Task<PagedResult<NpcSummaryDto>> ListAsync(string userId, NpcQuery query, CancellationToken ct);
    Task<NpcDto?> GetAsync(string userId, Guid id, CancellationToken ct);
    Task<ServiceResult<NpcDto>> CreateAsync(string userId, CreateNpcRequest request, CancellationToken ct);
    Task<ServiceResult<NpcDto>> UpdateAsync(string userId, Guid id, UpdateNpcRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct);
}
