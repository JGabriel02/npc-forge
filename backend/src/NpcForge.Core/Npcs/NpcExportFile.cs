using System.ComponentModel.DataAnnotations;

namespace NpcForge.Core.Npcs;

/// <summary>
/// Um NPC dentro do arquivo de exportação: o conteúdo, sem nada que só faz sentido no banco de origem
/// (id, campanha, datas, dono).
/// </summary>
public record ExportedNpc(
    [Required, MaxLength(NpcLimits.ShortText)] string Name,
    [MaxLength(NpcLimits.ShortText)] string? Race,
    [MaxLength(NpcLimits.ShortText)] string? Occupation,
    [MaxLength(NpcLimits.LongText)] string? Appearance,
    [MaxLength(NpcLimits.LongText)] string? Personality,
    [MaxLength(NpcLimits.VeryLongText)] string? Background,
    [MaxLength(NpcLimits.VeryLongText)] string? Notes,
    StatBlockRequest? StatBlock);

/// <summary>Arquivo de exportação de NPCs (formato "npcforge-npcs").</summary>
public class NpcExportFile
{
    public const string FormatName = "npcforge-npcs";
    public const int CurrentVersion = 1;

    public string Format { get; set; } = FormatName;
    public int Version { get; set; } = CurrentVersion;
    public DateTime ExportedAt { get; set; }
    public List<ExportedNpc>? Npcs { get; set; }
}
