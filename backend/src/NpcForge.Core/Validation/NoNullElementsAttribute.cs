using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace NpcForge.Core.Validation;

/// <summary>Rejeita listas com itens <c>null</c> (a validação do MVC não entra em itens nulos).</summary>
public class NoNullElementsAttribute : ValidationAttribute
{
    public NoNullElementsAttribute() : base("A lista não pode conter itens vazios.") { }

    public override bool IsValid(object? value) => value is not IEnumerable items || items.Cast<object?>().All(i => i is not null);
}
