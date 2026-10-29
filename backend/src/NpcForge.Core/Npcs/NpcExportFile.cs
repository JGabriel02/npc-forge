using System.ComponentModel.DataAnnotations;
using NpcForge.Core.Validation;

namespace NpcForge.Core.Npcs;

/// <summary>
/// Um NPC dentro do arquivo de exportação: o conteúdo, sem nada que só faz sentido no banco de origem
/// (id, campanha, datas, dono).
/// </summary>
public record ExportedNpc(
    [Display(Name = "Nome"), Required(ErrorMessage = Messages.Required), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string Name,
    [Display(Name = "Raça"), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string? Race,
    [Display(Name = "Profissão"), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string? Occupation,
    [Display(Name = "Aparência"), MaxLength(NpcLimits.LongText, ErrorMessage = Messages.MaxLength)] string? Appearance,
    [Display(Name = "Personalidade"), MaxLength(NpcLimits.LongText, ErrorMessage = Messages.MaxLength)] string? Personality,
    [Display(Name = "Histórico"), MaxLength(NpcLimits.VeryLongText, ErrorMessage = Messages.MaxLength)] string? Background,
    [Display(Name = "Anotações"), MaxLength(NpcLimits.VeryLongText, ErrorMessage = Messages.MaxLength)] string? Notes,
    StatBlockRequest? StatBlock);

/// <summary>
/// Arquivo de exportação de NPCs (formato "npcforge-npcs"). Na importação, o ASP.NET valida tudo
/// (inclusive cada NPC, com as mesmas regras do cadastro) antes de qualquer gravação.
/// </summary>
public class NpcExportFile
{
    public const string FormatName = "npcforge-npcs";
    public const int CurrentVersion = 1;
    public const int MaxNpcs = 500;

    /// <summary>Tamanho máximo do arquivo: a importação recusa (413) e a exportação não gera nada acima disso.</summary>
    public const int MaxFileBytes = 20 * 1024 * 1024;

    // Sem valor padrão: um JSON sem "format"/"version" não é tratado como exportação do NpcForge.
    [Required(ErrorMessage = "Este arquivo não é uma exportação de NPCs do NpcForge.")]
    [AllowedValues(FormatName, ErrorMessage = "Este arquivo não é uma exportação de NPCs do NpcForge.")]
    public string Format { get; set; } = "";

    [Range(1, CurrentVersion, ErrorMessage = "Este arquivo foi exportado por uma versão mais nova do NpcForge.")]
    public int Version { get; set; }

    public DateTime ExportedAt { get; set; }

    [Required(ErrorMessage = "O arquivo não tem NPCs.")]
    [MinLength(1, ErrorMessage = "O arquivo não tem NPCs.")]
    [MaxLength(MaxNpcs, ErrorMessage = "O arquivo tem mais de 500 NPCs.")]
    [NoNullElements]
    public List<ExportedNpc>? Npcs { get; set; }
}
