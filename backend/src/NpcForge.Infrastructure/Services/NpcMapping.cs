using NpcForge.Core.Entities;
using NpcForge.Core.Npcs;
using NpcForge.Core.Rules;

namespace NpcForge.Infrastructure.Services;

/// <summary>Conversões entre entidades e DTOs; calcula os valores derivados da ficha.</summary>
public static class NpcMapping
{
    public static NpcDto ToDto(this Npc n) => new(
        n.Id, n.CampaignId, n.Name, n.Race, n.Occupation, n.Appearance, n.Personality, n.Background, n.Notes,
        n.StatBlock?.ToDto(), n.CreatedAt, n.UpdatedAt);

    public static StatBlockDto ToDto(this StatBlock s) => new(
        s.Size, s.CreatureType, s.Alignment, s.ArmorClass, s.HitPoints, s.HitDice, s.Speed,
        s.Str, s.Dex, s.Con, s.Int, s.Wis, s.Cha, s.ChallengeRating,
        s.SavingThrows, s.Skills, s.Senses, s.Languages,
        s.Traits.Select(ToDto).ToList(),
        s.Actions.Select(a => new StatBlockActionDto(a.Name, a.Description, a.AttackBonus, a.Damage)).ToList(),
        s.Reactions.Select(ToDto).ToList(),
        new AbilityModifiers(
            DndRules.AbilityModifier(s.Str), DndRules.AbilityModifier(s.Dex), DndRules.AbilityModifier(s.Con),
            DndRules.AbilityModifier(s.Int), DndRules.AbilityModifier(s.Wis), DndRules.AbilityModifier(s.Cha)),
        DndRules.ProficiencyBonus(s.ChallengeRating));

    public static StatBlock ToEntity(this StatBlockRequest r, Guid npcId) => new()
    {
        NpcId = npcId,
        Size = r.Size, CreatureType = r.CreatureType, Alignment = r.Alignment,
        ArmorClass = r.ArmorClass, HitPoints = r.HitPoints, HitDice = r.HitDice, Speed = r.Speed,
        Str = r.Str, Dex = r.Dex, Con = r.Con, Int = r.Int, Wis = r.Wis, Cha = r.Cha,
        ChallengeRating = r.ChallengeRating,
        SavingThrows = r.SavingThrows, Skills = r.Skills, Senses = r.Senses, Languages = r.Languages,
        Traits = (r.Traits ?? []).Select(ToEntity).ToList(),
        Actions = (r.Actions ?? []).Select(a => new StatBlockAction
            { Name = a.Name, Description = a.Description, AttackBonus = a.AttackBonus, Damage = a.Damage }).ToList(),
        Reactions = (r.Reactions ?? []).Select(ToEntity).ToList(),
    };

    private static StatBlockFeatureDto ToDto(StatBlockFeature f) => new(f.Name, f.Description);

    private static StatBlockFeature ToEntity(StatBlockFeatureDto f) => new() { Name = f.Name, Description = f.Description };
}
