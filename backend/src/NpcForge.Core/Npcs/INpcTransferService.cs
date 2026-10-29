namespace NpcForge.Core.Npcs;

/// <summary>Exportação e importação de NPCs em arquivo (levar NPCs de uma instalação para outra).</summary>
public interface INpcTransferService
{
    /// <summary>Todos os NPCs do usuário que batem com os filtros da lista (sem paginação).</summary>
    Task<NpcExportFile> ExportAsync(string userId, NpcQuery filters, CancellationToken ct);

    /// <summary>Um NPC; <c>null</c> se não for do usuário.</summary>
    Task<NpcExportFile?> ExportOneAsync(string userId, Guid id, CancellationToken ct);

    /// <summary>
    /// Cria cópias dos NPCs do arquivo (já validado), sem campanha, numa única gravação: entram todos ou nenhum.
    /// </summary>
    Task<int> ImportAsync(string userId, NpcExportFile file, CancellationToken ct);
}
