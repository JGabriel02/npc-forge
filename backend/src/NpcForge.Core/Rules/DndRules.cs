using System.Text.RegularExpressions;

namespace NpcForge.Core.Rules;

/// <summary>Regras de D&D 5e usadas para validar e calcular valores derivados da ficha.</summary>
public static partial class DndRules
{
    public static IReadOnlyList<string> ChallengeRatings { get; } =
        ["0", "1/8", "1/4", "1/2", .. Enumerable.Range(1, 30).Select(n => n.ToString())];

    // Divisão inteira em C# trunca para zero; o modificador arredonda para baixo (score 1 → -5).
    public static int AbilityModifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    public static bool IsValidChallengeRating(string? cr) => cr is not null && ChallengeRatings.Contains(cr);

    public static int ProficiencyBonus(string cr)
    {
        if (!IsValidChallengeRating(cr))
            throw new ArgumentException($"Nível de desafio inválido: '{cr}'.", nameof(cr));

        // Frações (1/8, 1/4, 1/2) e ND 0–4 dão +2; a partir do 5 sobe +1 a cada 4 NDs.
        var value = cr.Contains('/') ? 0 : int.Parse(cr);
        return value < 5 ? 2 : 2 + (value - 1) / 4;
    }

    public static bool IsValidHitDice(string? hitDice) => hitDice is not null && HitDiceRegex().IsMatch(hitDice);

    [GeneratedRegex(@"^\d+d\d+([+-]\d+)?$")]
    private static partial Regex HitDiceRegex();
}
