using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NpcForge.Core.Ai;
using NpcForge.Infrastructure.Persistence;

namespace NpcForge.Infrastructure.Services;

public class AiUsageService(AppDbContext db, TimeProvider time, IOptions<AiOptions> options) : IAiUsageService
{
    private readonly int _limit = options.Value.DailyGenerationLimit;

    // 0 = sem limite: a reserva sempre passa, mas o contador continua registrando o uso.
    private int EffectiveLimit => _limit == 0 ? int.MaxValue : _limit;

    public async Task<AiUsageDto> GetAsync(string userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = AiUsageClock.Today(now);
        var used = await db.AiUsages
            .Where(u => u.UserId == userId && u.Date == today)
            .Select(u => u.GenerationCount)
            .FirstOrDefaultAsync(ct);
        return new AiUsageDto(used, _limit, AiUsageClock.ResetsAt(now));
    }

    public async Task<bool> TryReserveAsync(string userId, CancellationToken ct)
    {
        var today = AiUsageClock.Today(time.GetUtcNow());

        // Verificar e incrementar num comando só: requisições simultâneas não passam do limite.
        // Se o contador já está no limite, o WHERE impede o UPDATE e o RETURNING não devolve linha.
        var reserved = await db.Database.SqlQuery<int>($"""
            INSERT INTO "AiUsages" ("UserId", "Date", "GenerationCount")
            VALUES ({userId}, {today}, 1)
            ON CONFLICT ("UserId", "Date")
            DO UPDATE SET "GenerationCount" = "AiUsages"."GenerationCount" + 1
            WHERE "AiUsages"."GenerationCount" < {EffectiveLimit}
            RETURNING "GenerationCount" AS "Value"
            """).ToListAsync(ct);

        return reserved.Count > 0;
    }

    public async Task ReleaseAsync(string userId, CancellationToken ct)
    {
        var today = AiUsageClock.Today(time.GetUtcNow());

        // CancellationToken.None: a devolução precisa acontecer mesmo se a requisição foi cancelada.
        await db.Database.ExecuteSqlAsync($"""
            UPDATE "AiUsages" SET "GenerationCount" = "GenerationCount" - 1
            WHERE "UserId" = {userId} AND "Date" = {today} AND "GenerationCount" > 0
            """, CancellationToken.None);
    }
}
