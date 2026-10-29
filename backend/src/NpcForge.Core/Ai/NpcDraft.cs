using NpcForge.Core.Npcs;

namespace NpcForge.Core.Ai;

/// <summary>NPC sugerido pela IA. Não é salvo: preenche o formulário, e o usuário salva pelo fluxo normal.</summary>
public record NpcDraft(
    string Name,
    string? Race,
    string? Occupation,
    string? Appearance,
    string? Personality,
    string? Background,
    StatBlockRequest? StatBlock);

/// <summary>
/// Resultado da leitura da resposta do modelo. <see cref="ParseError"/> preenchido significa que nada
/// aproveitável foi extraído (o serviço tenta de novo, mandando o erro para o modelo).
/// </summary>
public record DraftResult(NpcDraft? Draft, IReadOnlyList<string> Warnings, string? ParseError)
{
    public static DraftResult Failed(string error) => new(null, [], error);
}
