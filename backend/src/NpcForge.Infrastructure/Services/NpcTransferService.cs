using Microsoft.EntityFrameworkCore;
using NpcForge.Core.Entities;
using NpcForge.Core.Npcs;
using NpcForge.Infrastructure.Persistence;

namespace NpcForge.Infrastructure.Services;

public class NpcTransferService(AppDbContext db, TimeProvider time) : INpcTransferService
{
    public async Task<NpcExportFile> ExportAsync(string userId, NpcQuery filters, CancellationToken ct)
    {
        var npcs = await NpcFiltering.Apply(Owned(userId), filters)
            .Include(n => n.StatBlock)
            .OrderByDescending(n => n.UpdatedAt).ThenBy(n => n.Id)
            .ToListAsync(ct);
        return ToFile(npcs);
    }

    public async Task<NpcExportFile?> ExportOneAsync(string userId, Guid id, CancellationToken ct)
    {
        var npc = await Owned(userId).Include(n => n.StatBlock).SingleOrDefaultAsync(n => n.Id == id, ct);
        return npc is null ? null : ToFile([npc]);
    }

    public async Task<int> ImportAsync(string userId, NpcExportFile file, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        foreach (var exported in file.Npcs!)
        {
            var npc = new Npc
            {
                Id = Guid.NewGuid(), UserId = userId, CampaignId = null,
                Name = exported.Name, Race = exported.Race, Occupation = exported.Occupation,
                Appearance = exported.Appearance, Personality = exported.Personality,
                Background = exported.Background, Notes = exported.Notes,
                CreatedAt = now, UpdatedAt = now,
            };
            npc.StatBlock = exported.StatBlock?.ToEntity(npc.Id); // normaliza os dados de vida
            db.Npcs.Add(npc);
        }

        // Um único SaveChanges roda numa transação: ou todos os NPCs entram, ou nenhum.
        await db.SaveChangesAsync(ct);
        return file.Npcs!.Count;
    }

    private IQueryable<Npc> Owned(string userId) => db.Npcs.AsNoTracking().Where(n => n.UserId == userId);

    private NpcExportFile ToFile(IEnumerable<Npc> npcs) => new()
    {
        Format = NpcExportFile.FormatName,
        Version = NpcExportFile.CurrentVersion,
        ExportedAt = time.GetUtcNow().UtcDateTime,
        Npcs = npcs.Select(n => n.ToExported()).ToList(),
    };
}
