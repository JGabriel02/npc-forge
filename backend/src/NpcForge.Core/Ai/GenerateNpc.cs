using System.ComponentModel.DataAnnotations;
using NpcForge.Core.Common;
using NpcForge.Core.Validation;

namespace NpcForge.Core.Ai;

public record GenerateNpcRequest(
    [Required, MaxLength(500)] string Prompt,
    Guid? CampaignId,
    bool WithStatBlock,
    [ChallengeRating] string? ChallengeRating);

public record GenerateNpcResponse(NpcDraft Draft, IReadOnlyList<string> Warnings, AiUsageDto Usage);

/// <summary>Gera um rascunho de NPC com IA, respeitando o limite diário do usuário. Não salva nada.</summary>
public interface INpcGenerationService
{
    Task<ServiceResult<GenerateNpcResponse>> GenerateAsync(string userId, GenerateNpcRequest request, CancellationToken ct);
}
