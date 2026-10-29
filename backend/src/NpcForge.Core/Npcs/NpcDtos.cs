using System.ComponentModel.DataAnnotations;
using NpcForge.Core.Entities;
using NpcForge.Core.Validation;

namespace NpcForge.Core.Npcs;

// [Display] dá o nome em português usado nas mensagens de validação ({0} em Messages).

public record StatBlockFeatureDto(
    [Display(Name = "Nome"), Required(ErrorMessage = Messages.Required), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string Name,
    [Display(Name = "Descrição"), Required(ErrorMessage = Messages.Required), MaxLength(NpcLimits.LongText, ErrorMessage = Messages.MaxLength)] string Description);

public record StatBlockActionDto(
    [Display(Name = "Nome"), Required(ErrorMessage = Messages.Required), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string Name,
    [Display(Name = "Descrição"), Required(ErrorMessage = Messages.Required), MaxLength(NpcLimits.LongText, ErrorMessage = Messages.MaxLength)] string Description,
    int? AttackBonus,
    [Display(Name = "Dano"), MaxLength(NpcLimits.MediumText, ErrorMessage = Messages.MaxLength)] string? Damage);

public record StatBlockRequest(
    [Display(Name = "Tamanho"), Required(ErrorMessage = Messages.Required), EnumDataType(typeof(CreatureSize))] CreatureSize? Size,
    [Display(Name = "Tipo de criatura"), Required(ErrorMessage = Messages.Required), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string CreatureType,
    [Display(Name = "Alinhamento"), Required(ErrorMessage = Messages.Required), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string Alignment,
    [Display(Name = "Classe de Armadura"), Range(1, 30, ErrorMessage = Messages.Range)] int ArmorClass,
    [Display(Name = "Pontos de vida"), Range(1, int.MaxValue, ErrorMessage = Messages.AtLeast)] int HitPoints,
    [Display(Name = "Dados de vida"), Required(ErrorMessage = Messages.Required), HitDice, MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string HitDice,
    [Display(Name = "Deslocamento"), Required(ErrorMessage = Messages.Required), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string Speed,
    [Display(Name = "Força"), Range(1, 30, ErrorMessage = Messages.Range)] int Str,
    [Display(Name = "Destreza"), Range(1, 30, ErrorMessage = Messages.Range)] int Dex,
    [Display(Name = "Constituição"), Range(1, 30, ErrorMessage = Messages.Range)] int Con,
    [Display(Name = "Inteligência"), Range(1, 30, ErrorMessage = Messages.Range)] int Int,
    [Display(Name = "Sabedoria"), Range(1, 30, ErrorMessage = Messages.Range)] int Wis,
    [Display(Name = "Carisma"), Range(1, 30, ErrorMessage = Messages.Range)] int Cha,
    [Display(Name = "Nível de desafio"), Required(ErrorMessage = Messages.Required), ChallengeRating] string ChallengeRating,
    [Display(Name = "Salvaguardas"), MaxLength(NpcLimits.MediumText, ErrorMessage = Messages.MaxLength)] string? SavingThrows,
    [Display(Name = "Perícias"), MaxLength(NpcLimits.MediumText, ErrorMessage = Messages.MaxLength)] string? Skills,
    [Display(Name = "Sentidos"), MaxLength(NpcLimits.MediumText, ErrorMessage = Messages.MaxLength)] string? Senses,
    [Display(Name = "Idiomas"), MaxLength(NpcLimits.MediumText, ErrorMessage = Messages.MaxLength)] string? Languages,
    [Display(Name = "Características"), NoNullElements, MaxLength(NpcLimits.StatBlockListItems, ErrorMessage = Messages.MaxItems)] List<StatBlockFeatureDto>? Traits,
    [Display(Name = "Ações"), NoNullElements, MaxLength(NpcLimits.StatBlockListItems, ErrorMessage = Messages.MaxItems)] List<StatBlockActionDto>? Actions,
    [Display(Name = "Reações"), NoNullElements, MaxLength(NpcLimits.StatBlockListItems, ErrorMessage = Messages.MaxItems)] List<StatBlockFeatureDto>? Reactions);

public record AbilityModifiers(int Str, int Dex, int Con, int Int, int Wis, int Cha);

public record StatBlockDto(
    CreatureSize Size, string CreatureType, string Alignment, int ArmorClass, int HitPoints, string HitDice,
    string Speed, int Str, int Dex, int Con, int Int, int Wis, int Cha, string ChallengeRating,
    string? SavingThrows, string? Skills, string? Senses, string? Languages,
    IReadOnlyList<StatBlockFeatureDto> Traits, IReadOnlyList<StatBlockActionDto> Actions,
    IReadOnlyList<StatBlockFeatureDto> Reactions,
    AbilityModifiers Modifiers, int ProficiencyBonus);

public record CreateNpcRequest(
    Guid? CampaignId,
    [Display(Name = "Nome"), Required(ErrorMessage = Messages.Required), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string Name,
    [Display(Name = "Raça"), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string? Race,
    [Display(Name = "Profissão"), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string? Occupation,
    [Display(Name = "Aparência"), MaxLength(NpcLimits.LongText, ErrorMessage = Messages.MaxLength)] string? Appearance,
    [Display(Name = "Personalidade"), MaxLength(NpcLimits.LongText, ErrorMessage = Messages.MaxLength)] string? Personality,
    [Display(Name = "Histórico"), MaxLength(NpcLimits.VeryLongText, ErrorMessage = Messages.MaxLength)] string? Background,
    [Display(Name = "Anotações"), MaxLength(NpcLimits.VeryLongText, ErrorMessage = Messages.MaxLength)] string? Notes,
    StatBlockRequest? StatBlock);

public record UpdateNpcRequest(
    Guid? CampaignId,
    [Display(Name = "Nome"), Required(ErrorMessage = Messages.Required), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string Name,
    [Display(Name = "Raça"), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string? Race,
    [Display(Name = "Profissão"), MaxLength(NpcLimits.ShortText, ErrorMessage = Messages.MaxLength)] string? Occupation,
    [Display(Name = "Aparência"), MaxLength(NpcLimits.LongText, ErrorMessage = Messages.MaxLength)] string? Appearance,
    [Display(Name = "Personalidade"), MaxLength(NpcLimits.LongText, ErrorMessage = Messages.MaxLength)] string? Personality,
    [Display(Name = "Histórico"), MaxLength(NpcLimits.VeryLongText, ErrorMessage = Messages.MaxLength)] string? Background,
    [Display(Name = "Anotações"), MaxLength(NpcLimits.VeryLongText, ErrorMessage = Messages.MaxLength)] string? Notes);

public record NpcDto(
    Guid Id, Guid? CampaignId, string Name, string? Race, string? Occupation, string? Appearance,
    string? Personality, string? Background, string? Notes, StatBlockDto? StatBlock,
    DateTime CreatedAt, DateTime UpdatedAt);

public record NpcSummaryDto(
    Guid Id, string Name, string? Race, string? Occupation, Guid? CampaignId, string? CampaignName,
    bool HasStatBlock, string? ChallengeRating, DateTime UpdatedAt);
