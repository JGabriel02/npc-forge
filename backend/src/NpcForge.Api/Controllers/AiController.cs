using Microsoft.AspNetCore.Mvc;
using NpcForge.Api.Auth;
using NpcForge.Core.Ai;
using NpcForge.Core.Common;

namespace NpcForge.Api.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController(INpcGenerationService generation, IAiUsageService usage) : ControllerBase
{
    /// <summary>Gera um rascunho de NPC (não salva). Consome uma geração do limite diário se der certo.</summary>
    [HttpPost("generate-npc")]
    public async Task<ActionResult<GenerateNpcResponse>> GenerateNpc(GenerateNpcRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await generation.GenerateAsync(userId, request, ct);
        if (result.Error is null)
            return result.Value!;

        switch (result.Error)
        {
            case ServiceError.InvalidCampaign:
                ModelState.AddModelError("campaignId", "Campanha não encontrada.");
                return ValidationProblem(ModelState);

            case ServiceError.DailyLimitReached:
                var problem = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext, StatusCodes.Status429TooManyRequests, "Você atingiu o limite de gerações de hoje.");
                problem.Extensions["resetsAt"] = (await usage.GetAsync(userId, ct)).ResetsAt;
                return new ObjectResult(problem) { StatusCode = StatusCodes.Status429TooManyRequests };

            case ServiceError.AiTimeout:
                return Problem(statusCode: StatusCodes.Status504GatewayTimeout,
                    title: "A IA demorou demais para responder. Tente novamente.");

            default:
                return Problem(statusCode: StatusCodes.Status502BadGateway,
                    title: result.Message ?? "A IA não conseguiu gerar agora. Tente novamente.");
        }
    }

    [HttpGet("usage")]
    public Task<AiUsageDto> Usage(CancellationToken ct) => usage.GetAsync(User.GetUserId(), ct);
}
