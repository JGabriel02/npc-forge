using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace NpcForge.Api.Auth;

/// <summary>A claim que marca o token da conta de visitante.</summary>
public static class DemoClaims
{
    public const string Type = "demo";

    public static bool IsDemo(this ClaimsPrincipal user) => user.HasClaim(Type, "true");
}

/// <summary>
/// Visitante só lê: qualquer POST, PUT ou DELETE com token demo recebe 403 antes de o controller rodar.
/// Esconder botões no Angular não basta, porque qualquer um pode chamar a API direto.
/// </summary>
public class DemoReadOnlyFilter(ProblemDetailsFactory problems) : IAsyncResourceFilter
{
    public Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var reads = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)
            || HttpMethods.IsOptions(http.Request.Method);
        var anonymous = context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (reads || anonymous || !http.User.IsDemo()) return next();

        var problem = problems.CreateProblemDetails(http, StatusCodes.Status403Forbidden,
            "A conta de visitante é só para leitura. Crie uma conta para salvar seus NPCs.");
        context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
        return Task.CompletedTask;
    }
}
