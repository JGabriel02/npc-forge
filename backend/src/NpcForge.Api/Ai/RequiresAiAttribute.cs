using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using NpcForge.Core.Ai;

namespace NpcForge.Api.Ai;

/// <summary>
/// Com a IA desligada (Ai:Enabled = false), responde 404 antes de o controller ser criado.
/// Isso importa porque, desligada, os serviços de IA nem são registrados no DI.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequiresAiAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => true;

    public IFilterMetadata CreateInstance(IServiceProvider services)
        => new Filter(
            services.GetRequiredService<IOptions<AiOptions>>().Value.Enabled,
            services.GetRequiredService<ProblemDetailsFactory>());

    private sealed class Filter(bool enabled, ProblemDetailsFactory problems) : IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            if (enabled) return;

            var problem = problems.CreateProblemDetails(
                context.HttpContext, StatusCodes.Status404NotFound, "A IA está desativada nesta instalação.");
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status404NotFound };
        }

        public void OnResourceExecuted(ResourceExecutedContext context) { }
    }
}
