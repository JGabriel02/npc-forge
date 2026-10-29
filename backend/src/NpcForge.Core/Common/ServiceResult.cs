namespace NpcForge.Core.Common;

public enum ServiceError
{
    NotFound,
    InvalidCampaign,
    DailyLimitReached,
    AiUnavailable,
    AiTimeout,
}

/// <summary>Resultado de uma operação que pode falhar por motivo de negócio (não exceção).</summary>
public record ServiceResult<T>(T? Value, ServiceError? Error, string? Message = null)
{
    public static ServiceResult<T> Ok(T value) => new(value, null);
    public static ServiceResult<T> Fail(ServiceError error, string? message = null) => new(default, error, message);
}
