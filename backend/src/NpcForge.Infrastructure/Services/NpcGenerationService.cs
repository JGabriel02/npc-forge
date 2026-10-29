using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NpcForge.Core.Ai;
using NpcForge.Core.Common;
using NpcForge.Infrastructure.Persistence;

namespace NpcForge.Infrastructure.Services;

public class NpcGenerationService(
    AppDbContext db,
    IAiUsageService usage,
    IAiProvider ai,
    PromptBuilder prompts,
    IOptions<AiOptions> options,
    ILogger<NpcGenerationService> logger) : INpcGenerationService
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

    public async Task<ServiceResult<GenerateNpcResponse>> GenerateAsync(
        string userId, GenerateNpcRequest request, CancellationToken ct)
    {
        string? setting = null;
        if (request.CampaignId is { } campaignId)
        {
            var campaign = await db.Campaigns
                .Where(c => c.Id == campaignId && c.UserId == userId)
                .Select(c => new { c.Setting })
                .FirstOrDefaultAsync(ct);
            if (campaign is null)
                return Fail(ServiceError.InvalidCampaign);
            setting = campaign.Setting;
        }

        if (!await usage.TryReserveAsync(userId, ct))
            return Fail(ServiceError.DailyLimitReached);

        var succeeded = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_timeout);

            var requestedCr = request.WithStatBlock ? request.ChallengeRating : null;
            var prompt = prompts.BuildGenerateNpc(request.Prompt, setting, request.WithStatBlock, requestedCr);

            var result = await AskAsync(prompt, request.WithStatBlock, requestedCr, timeout.Token);
            if (result.ParseError is not null)
                result = await AskAsync(prompts.BuildRetry(prompt, result.ParseError), request.WithStatBlock, requestedCr, timeout.Token);
            if (result.ParseError is not null)
                return Fail(ServiceError.AiUnavailable, "A IA não conseguiu gerar agora. Tente novamente.");

            succeeded = true;
            var currentUsage = await usage.GetAsync(userId, CancellationToken.None);
            return ServiceResult<GenerateNpcResponse>.Ok(new GenerateNpcResponse(result.Draft!, result.Warnings, currentUsage));
        }
        catch (AiProviderException ex)
        {
            return Fail(ServiceError.AiUnavailable, ex.Message);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Cancelado pelo nosso CancelAfter, não pelo cliente: estourou o tempo.
            return Fail(ServiceError.AiTimeout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Rede de segurança: nada vindo do provedor ou da resposta do modelo pode virar erro 500.
            logger.LogWarning(ex, "Falha inesperada ao gerar NPC com IA.");
            return Fail(ServiceError.AiUnavailable);
        }
        finally
        {
            // Qualquer falha (inclusive o cliente desistir) devolve a geração do dia.
            if (!succeeded)
                await usage.ReleaseAsync(userId, CancellationToken.None);
        }
    }

    private async Task<DraftResult> AskAsync(AiPrompt prompt, bool withStatBlock, string? requestedCr, CancellationToken ct)
    {
        var raw = await ai.CompleteJsonAsync(prompt.System, prompt.User, prompt.JsonSchema, ct);
        return NpcDraftValidator.Parse(raw, withStatBlock, requestedCr);
    }

    private static ServiceResult<GenerateNpcResponse> Fail(ServiceError error, string? message = null)
        => ServiceResult<GenerateNpcResponse>.Fail(error, message);
}
