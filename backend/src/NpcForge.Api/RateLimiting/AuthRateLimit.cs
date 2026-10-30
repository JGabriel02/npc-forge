using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace NpcForge.Api.RateLimiting;

/// <summary>
/// Limite de tentativas por IP em cadastro, login e visitante: freia robôs criando contas e
/// tentativas de adivinhar senhas. O IP vem do X-Forwarded-For (ver UseForwardedHeaders no Program).
/// </summary>
public static class AuthRateLimit
{
    public const string Policy = "auth";

    public static IServiceCollection AddAuthRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        var perMinute = configuration.GetValue("RateLimit:AuthPerMinute", 10);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Policy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = async (context, ct) =>
            {
                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Muitas tentativas. Aguarde um minuto e tente de novo." },
                });
            };
        });
    }
}
