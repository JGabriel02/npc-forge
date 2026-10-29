namespace NpcForge.Core.Ai;

public record AiUsageDto(int Used, int Limit, DateTime ResetsAt);

/// <summary>O "dia" do limite é o dia em UTC; o contador zera à meia-noite UTC.</summary>
public static class AiUsageClock
{
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    public static DateTime ResetsAt(DateTimeOffset now)
        => Today(now).AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}

/// <summary>Limite diário de gerações por IA, por usuário.</summary>
public interface IAiUsageService
{
    Task<AiUsageDto> GetAsync(string userId, CancellationToken ct);

    /// <summary>Reserva uma geração de forma atômica. <c>false</c> = limite do dia atingido.</summary>
    Task<bool> TryReserveAsync(string userId, CancellationToken ct);

    /// <summary>Devolve uma reserva (a geração falhou ou foi cancelada).</summary>
    Task ReleaseAsync(string userId, CancellationToken ct);
}
