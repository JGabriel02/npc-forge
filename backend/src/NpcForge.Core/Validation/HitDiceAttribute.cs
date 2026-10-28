using System.ComponentModel.DataAnnotations;
using NpcForge.Core.Rules;

namespace NpcForge.Core.Validation;

/// <summary>Aceita notação de dados de vida como "4d8+4". <c>null</c> fica a cargo de [Required].</summary>
public class HitDiceAttribute : ValidationAttribute
{
    public HitDiceAttribute() : base("Dados de vida inválidos. Use o formato 4d8+4.") { }

    public override bool IsValid(object? value) => value is null || DndRules.IsValidHitDice(value as string);
}
