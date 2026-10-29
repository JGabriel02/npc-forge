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

    /// <summary>Média de PV como nos livros: floor(qtd × (lados + 1) / 2) + modificador, nunca menos que 1.</summary>
    public static int AverageHitPoints(string hitDice)
    {
        var match = HitDiceRegex().Match(hitDice);
        if (!match.Success)
            throw new ArgumentException($"Dados de vida inválidos: '{hitDice}'.", nameof(hitDice));

        var count = int.Parse(match.Groups["count"].Value);
        var sides = int.Parse(match.Groups["sides"].Value);
        var modifier = match.Groups["sign"].Success ? int.Parse(match.Groups["mod"].Value) : 0;
        if (match.Groups["sign"].Value is not ("" or "+")) modifier = -modifier;

        return Math.Max(1, count * (sides + 1) / 2 + modifier);
    }

    /// <summary>"1/8" → 0,125; "12" → 12.</summary>
    public static double ChallengeRatingValue(string cr)
    {
        if (!IsValidChallengeRating(cr))
            throw new ArgumentException($"Nível de desafio inválido: '{cr}'.", nameof(cr));

        return cr switch
        {
            "1/8" => 0.125,
            "1/4" => 0.25,
            "1/2" => 0.5,
            _ => int.Parse(cr),
        };
    }

    /// <summary>ND oficial mais próximo de um valor qualquer; em empate, o menor.</summary>
    public static string NearestChallengeRating(double value)
        => ChallengeRatings.MinBy(cr => (Math.Abs(ChallengeRatingValue(cr) - value), ChallengeRatingValue(cr)))!;

    // Sinais aceitos: + e os três "menos" que aparecem em textos: hífen (-), travessão (–) e menos tipográfico (−).
    // Números com no máximo 4 dígitos (5 no modificador): nenhuma ficha real chega perto, e assim int.Parse nunca estoura.
    [GeneratedRegex(@"^\s*(?<count>\d{1,4})\s*[dD]\s*(?<sides>\d{1,4})\s*(?:(?<sign>[+\-–−])\s*(?<mod>\d{1,5}))?\s*$")]
    private static partial Regex HitDiceRegex();
}
