using System.Globalization;
using System.Text;

namespace NpcForge.Core.Npcs;

/// <summary>Nomes dos arquivos de exportação: só letras sem acento, números e hífens.</summary>
public static class FileNames
{
    private const int MaxSlugLength = 50;

    public static string ForList(DateOnly day) => $"npcs-{day:yyyy-MM-dd}.json";

    public static string ForNpc(string npcName) => Slug(npcName) is { Length: > 0 } slug ? $"npc-{slug}.json" : "npc.json";

    /// <summary>"Ireena Kolyana!" → "ireena-kolyana"; "Ñandú" → "nandu".</summary>
    public static string Slug(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue; // acento separado da letra pela normalização FormD
            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }

        var slug = builder.ToString();
        if (slug.Length > MaxSlugLength) slug = slug[..MaxSlugLength];
        return slug.Trim('-');
    }
}
