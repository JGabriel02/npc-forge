using System.ComponentModel.DataAnnotations;
using NpcForge.Core.Rules;

namespace NpcForge.Core.Validation;

/// <summary>Aceita apenas NDs oficiais (0, 1/8, 1/4, 1/2, 1–30). <c>null</c> fica a cargo de [Required].</summary>
public class ChallengeRatingAttribute : ValidationAttribute
{
    public ChallengeRatingAttribute() : base("Nível de desafio inválido. Use 0, 1/8, 1/4, 1/2 ou 1–30.") { }

    public override bool IsValid(object? value) => value is null || DndRules.IsValidChallengeRating(value as string);
}
