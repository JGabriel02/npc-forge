namespace NpcForge.Core.Common;

public enum ServiceError
{
    NotFound,
    InvalidCampaign,
}

/// <summary>Resultado de uma operação que pode falhar por motivo de negócio (não exceção).</summary>
public record ServiceResult<T>(T? Value, ServiceError? Error)
{
    public static ServiceResult<T> Ok(T value) => new(value, null);
    public static ServiceResult<T> Fail(ServiceError error) => new(default, error);
}
