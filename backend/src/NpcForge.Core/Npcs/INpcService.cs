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

    /// <summary>Cria ou substitui a ficha. <c>null</c> = NPC não encontrado.</summary>
    Task<NpcDto?> SetStatBlockAsync(string userId, Guid npcId, StatBlockRequest request, CancellationToken ct);

    /// <summary>Remove a ficha (sem ficha é no-op). <c>null</c> = NPC não encontrado.</summary>
    Task<NpcDto?> RemoveStatBlockAsync(string userId, Guid npcId, CancellationToken ct);
}
