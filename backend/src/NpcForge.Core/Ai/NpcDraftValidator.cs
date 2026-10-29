using System.Globalization;
using System.Text.Json;
using NpcForge.Core.Entities;
using NpcForge.Core.Npcs;
using NpcForge.Core.Rules;

namespace NpcForge.Core.Ai;

/// <summary>
/// Lê a resposta do modelo, aproveitando o que der e corrigindo o que estiver fora das regras.
/// Código puro, sem IA: cada correção vira um aviso em português para o usuário revisar.
/// </summary>
public static class NpcDraftValidator
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly Dictionary<string, CreatureSize> SizeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["miúdo"] = CreatureSize.Tiny, ["miudo"] = CreatureSize.Tiny,
        ["pequeno"] = CreatureSize.Small,
        ["médio"] = CreatureSize.Medium, ["medio"] = CreatureSize.Medium,
        ["grande"] = CreatureSize.Large,
        ["enorme"] = CreatureSize.Huge,
        ["imenso"] = CreatureSize.Gargantuan,
    };

    private static readonly (string Key, string Label)[] Abilities =
    [
        ("str", "Força"), ("dex", "Destreza"), ("con", "Constituição"),
        ("int", "Inteligência"), ("wis", "Sabedoria"), ("cha", "Carisma"),
    ];

    public static DraftResult Parse(string rawModelOutput, bool withStatBlock, string? requestedChallengeRating)
    {
        var start = rawModelOutput.IndexOf('{');
        var end = rawModelOutput.LastIndexOf('}');
        if (start < 0 || end <= start)
            return DraftResult.Failed("A resposta não contém um objeto JSON.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawModelOutput[start..(end + 1)], JsonOptions);
        }
        catch (JsonException ex)
        {
            return DraftResult.Failed($"JSON inválido: {ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return DraftResult.Failed("A resposta precisa ser um objeto JSON.");

            var warnings = new List<string>();
            var name = Text(root, "name", "Nome", NpcLimits.ShortText, warnings);
            if (name is null)
                return DraftResult.Failed("O campo \"name\" está vazio.");

            StatBlockRequest? statBlock = null;
            if (withStatBlock)
            {
                if (Property(root, "statBlock") is not { ValueKind: JsonValueKind.Object } sb)
                    return DraftResult.Failed("Faltou a ficha de combate (\"statBlock\").");
                statBlock = ReadStatBlock(sb, requestedChallengeRating, warnings);
            }

            var draft = new NpcDraft(
                name,
                Text(root, "race", "Raça", NpcLimits.ShortText, warnings),
                Text(root, "occupation", "Profissão", NpcLimits.ShortText, warnings),
                Text(root, "appearance", "Aparência", NpcLimits.LongText, warnings),
                Text(root, "personality", "Personalidade", NpcLimits.LongText, warnings),
                Text(root, "background", "Histórico", NpcLimits.VeryLongText, warnings),
                statBlock);

            return new DraftResult(draft, warnings, null);
        }
    }

    private static StatBlockRequest ReadStatBlock(JsonElement sb, string? requestedCr, List<string> warnings)
    {
        var size = ReadSize(sb, warnings);
        var creatureType = Text(sb, "creatureType", "Tipo de criatura", NpcLimits.ShortText, warnings)
                           ?? Filled("Tipo de criatura", "humanoide", warnings);
        var alignment = Text(sb, "alignment", "Alinhamento", NpcLimits.ShortText, warnings)
                        ?? Filled("Alinhamento", "neutro", warnings);
        var speed = Text(sb, "speed", "Deslocamento", NpcLimits.ShortText, warnings)
                    ?? Filled("Deslocamento", "9 m", warnings);

        var armorClass = Clamped(sb, "armorClass", "Classe de Armadura", 1, 30, fallback: 10, warnings);
        var scores = Abilities.ToDictionary(a => a.Key, a => Clamped(sb, a.Key, a.Label, 1, 30, fallback: 10, warnings));

        var hitDiceRaw = Text(sb, "hitDice", "Dados de vida", NpcLimits.ShortText, warnings);
        string hitDice;
        if (DndRules.IsValidHitDice(hitDiceRaw))
        {
            hitDice = DndRules.NormalizeHitDice(hitDiceRaw!);
        }
        else
        {
            hitDice = "";
            warnings.Add($"Dados de vida \"{hitDiceRaw}\" inválidos: informe no formato 2d8 + 2.");
        }

        var hitPoints = Number(Property(sb, "hitPoints"));
        if (hitPoints is null or <= 0)
        {
            hitPoints = hitDice != "" ? DndRules.AverageHitPoints(hitDice) : 1;
            warnings.Add($"Ajustamos Pontos de vida para {hitPoints} (média dos dados de vida).");
        }

        return new StatBlockRequest(
            size, creatureType, alignment, armorClass, hitPoints.Value, hitDice, speed,
            scores["str"], scores["dex"], scores["con"], scores["int"], scores["wis"], scores["cha"],
            ReadChallengeRating(sb, requestedCr, warnings),
            Text(sb, "savingThrows", "Salvaguardas", NpcLimits.MediumText, warnings),
            Text(sb, "skills", "Perícias", NpcLimits.MediumText, warnings),
            Text(sb, "senses", "Sentidos", NpcLimits.MediumText, warnings),
            Text(sb, "languages", "Idiomas", NpcLimits.MediumText, warnings),
            ReadItems(sb, "traits", "Características", warnings, ToFeature),
            ReadItems(sb, "actions", "Ações", warnings, ToAction),
            ReadItems(sb, "reactions", "Reações", warnings, ToFeature));
    }

    private static CreatureSize ReadSize(JsonElement sb, List<string> warnings)
    {
        var raw = Text(Property(sb, "size"));
        if (raw is not null && Enum.TryParse<CreatureSize>(raw, ignoreCase: true, out var size) && Enum.IsDefined(size))
            return size;
        if (raw is not null && SizeAliases.TryGetValue(raw, out size))
            return size;

        warnings.Add($"Ajustamos Tamanho: \"{raw}\" → Médio.");
        return CreatureSize.Medium;
    }

    private static string ReadChallengeRating(JsonElement sb, string? requestedCr, List<string> warnings)
    {
        var raw = Text(Property(sb, "challengeRating"));
        string cr;
        if (DndRules.IsValidChallengeRating(raw))
        {
            cr = raw!;
        }
        else
        {
            var value = ParseDouble(raw);
            cr = value is null ? requestedCr ?? "0" : DndRules.NearestChallengeRating(value.Value);
            warnings.Add($"Ajustamos Nível de desafio: \"{raw}\" → {cr}.");
        }

        if (requestedCr is not null && cr != requestedCr)
        {
            warnings.Add($"Ajustamos Nível de desafio: {cr} → {requestedCr} (o ND pedido).");
            cr = requestedCr;
        }
        return cr;
    }

    private static List<T> ReadItems<T>(
        JsonElement sb, string key, string label, List<string> warnings, Func<JsonElement, List<string>, T?> read)
        where T : class
    {
        if (Property(sb, key) is not { ValueKind: JsonValueKind.Array } array)
            return [];

        var items = new List<T>();
        var dropped = false;
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object && read(element, warnings) is { } item)
                items.Add(item);
            else
                dropped = true;
        }

        if (dropped)
            warnings.Add($"Descartamos itens sem nome em {label}.");
        if (items.Count > NpcLimits.StatBlockListItems)
        {
            warnings.Add($"Mantivemos só os {NpcLimits.StatBlockListItems} primeiros itens de {label}.");
            items = items.Take(NpcLimits.StatBlockListItems).ToList();
        }
        return items;
    }

    private static StatBlockFeatureDto? ToFeature(JsonElement item, List<string> warnings)
    {
        var name = Text(item, "name", "Nome do item", NpcLimits.ShortText, warnings);
        return name is null ? null
            : new StatBlockFeatureDto(name, Text(item, "description", "Descrição do item", NpcLimits.LongText, warnings) ?? "");
    }

    private static StatBlockActionDto? ToAction(JsonElement item, List<string> warnings)
    {
        var name = Text(item, "name", "Nome da ação", NpcLimits.ShortText, warnings);
        return name is null ? null
            : new StatBlockActionDto(
                name,
                Text(item, "description", "Descrição da ação", NpcLimits.LongText, warnings) ?? "",
                Number(Property(item, "attackBonus")),
                Text(item, "damage", "Dano", NpcLimits.MediumText, warnings));
    }

    // ---- leitura tolerante ----

    private static JsonElement? Property(JsonElement obj, string name)
    {
        foreach (var property in obj.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }
        return null;
    }

    private static string? Text(JsonElement? element) => element?.ValueKind switch
    {
        JsonValueKind.String => element.Value.GetString()!.Trim() is { Length: > 0 } s ? s : null,
        JsonValueKind.Number => element.Value.GetRawText(),
        _ => null,
    };

    /// <summary>Texto opcional: vazio vira null; acima do limite é cortado (com aviso).</summary>
    private static string? Text(JsonElement obj, string key, string label, int maxLength, List<string> warnings)
    {
        var text = Text(Property(obj, key));
        if (text is null || text.Length <= maxLength)
            return text;

        warnings.Add($"Ajustamos {label}: texto cortado em {maxLength} caracteres.");
        return text[..maxLength];
    }

    private static int? Number(JsonElement? element)
    {
        var value = element?.ValueKind switch
        {
            // TryGetDouble: números como 1e400 não cabem em double (GetDouble lançaria exceção).
            JsonValueKind.Number => element.Value.TryGetDouble(out var d) ? d : null,
            JsonValueKind.String => ParseDouble(element.Value.GetString()),
            _ => null,
        };
        return value is null || !double.IsFinite(value.Value) ? null : (int)Math.Round(value.Value);
    }

    private static double? ParseDouble(string? text)
        => double.TryParse(text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static int Clamped(JsonElement obj, string key, string label, int min, int max, int fallback, List<string> warnings)
    {
        var value = Number(Property(obj, key));
        if (value is null)
        {
            warnings.Add($"Preenchemos {label} com {fallback}.");
            return fallback;
        }

        var clamped = Math.Clamp(value.Value, min, max);
        if (clamped != value)
            warnings.Add($"Ajustamos {label}: {value} → {clamped}.");
        return clamped;
    }

    private static string Filled(string label, string value, List<string> warnings)
    {
        warnings.Add($"Preenchemos {label} com \"{value}\".");
        return value;
    }
}
