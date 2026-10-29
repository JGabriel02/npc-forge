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

    // Aceita "2d8+2" e também o formato do Livro do Jogador 2024: "2d8 + 2", "1d4 – 1" (travessão).
    public static bool IsValidHitDice(string? hitDice) => hitDice is not null && HitDiceRegex().IsMatch(hitDice);

    /// <summary>Padroniza no formato do livro: "2d8 + 2", "1d4 – 1" ou "3d8" (modificador 0 é omitido).</summary>
    public static string NormalizeHitDice(string hitDice)
    {
        var match = HitDiceRegex().Match(hitDice);
        if (!match.Success)
            throw new ArgumentException($"Dados de vida inválidos: '{hitDice}'.", nameof(hitDice));

        var dice = $"{int.Parse(match.Groups["count"].Value)}d{int.Parse(match.Groups["sides"].Value)}";
        if (!match.Groups["sign"].Success)
            return dice;

        var modifier = int.Parse(match.Groups["mod"].Value);
        if (modifier == 0)
            return dice;

        return match.Groups["sign"].Value == "+" ? $"{dice} + {modifier}" : $"{dice} – {modifier}";
    }

    // Sinais aceitos: + e os três "menos" que aparecem em textos: hífen (-), travessão (–) e menos tipográfico (−).
    [GeneratedRegex(@"^\s*(?<count>\d+)\s*[dD]\s*(?<sides>\d+)\s*(?:(?<sign>[+\-–−])\s*(?<mod>\d+))?\s*$")]
    private static partial Regex HitDiceRegex();
}
