using System.ComponentModel.DataAnnotations;
using NpcForge.Core.Entities;
using NpcForge.Core.Validation;

namespace NpcForge.Core.Npcs;

public record StatBlockFeatureDto(
    [Required, MaxLength(NpcLimits.ShortText)] string Name,
    [MaxLength(NpcLimits.LongText)] string Description);

public record StatBlockActionDto(
    [Required, MaxLength(NpcLimits.ShortText)] string Name,
    [MaxLength(NpcLimits.LongText)] string Description,
    int? AttackBonus,
    [MaxLength(NpcLimits.MediumText)] string? Damage);

public record StatBlockRequest(
    CreatureSize Size,
    [Required, MaxLength(NpcLimits.ShortText)] string CreatureType,
    [Required, MaxLength(NpcLimits.ShortText)] string Alignment,
    [Range(1, 30)] int ArmorClass,
    [Range(1, int.MaxValue)] int HitPoints,
    [Required, HitDice, MaxLength(NpcLimits.ShortText)] string HitDice,
    [Required, MaxLength(NpcLimits.ShortText)] string Speed,
    [Range(1, 30)] int Str,
    [Range(1, 30)] int Dex,
    [Range(1, 30)] int Con,
    [Range(1, 30)] int Int,
    [Range(1, 30)] int Wis,
    [Range(1, 30)] int Cha,
    [Required, ChallengeRating] string ChallengeRating,
    [MaxLength(NpcLimits.MediumText)] string? SavingThrows,
    [MaxLength(NpcLimits.MediumText)] string? Skills,
    [MaxLength(NpcLimits.MediumText)] string? Senses,
    [MaxLength(NpcLimits.MediumText)] string? Languages,
    List<StatBlockFeatureDto>? Traits,
    List<StatBlockActionDto>? Actions,
    List<StatBlockFeatureDto>? Reactions);

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
    [Required, MaxLength(NpcLimits.ShortText)] string Name,
    [MaxLength(NpcLimits.ShortText)] string? Race,
    [MaxLength(NpcLimits.ShortText)] string? Occupation,
    [MaxLength(NpcLimits.LongText)] string? Appearance,
    [MaxLength(NpcLimits.LongText)] string? Personality,
    [MaxLength(NpcLimits.VeryLongText)] string? Background,
    [MaxLength(NpcLimits.VeryLongText)] string? Notes,
    StatBlockRequest? StatBlock);

public record UpdateNpcRequest(
    Guid? CampaignId,
    [Required, MaxLength(NpcLimits.ShortText)] string Name,
    [MaxLength(NpcLimits.ShortText)] string? Race,
    [MaxLength(NpcLimits.ShortText)] string? Occupation,
    [MaxLength(NpcLimits.LongText)] string? Appearance,
    [MaxLength(NpcLimits.LongText)] string? Personality,
    [MaxLength(NpcLimits.VeryLongText)] string? Background,
    [MaxLength(NpcLimits.VeryLongText)] string? Notes);

public record NpcDto(
    Guid Id, Guid? CampaignId, string Name, string? Race, string? Occupation, string? Appearance,
    string? Personality, string? Background, string? Notes, StatBlockDto? StatBlock,
    DateTime CreatedAt, DateTime UpdatedAt);

public record NpcSummaryDto(
    Guid Id, string Name, string? Race, string? Occupation, Guid? CampaignId, string? CampaignName,
    bool HasStatBlock, string? ChallengeRating, DateTime UpdatedAt);
