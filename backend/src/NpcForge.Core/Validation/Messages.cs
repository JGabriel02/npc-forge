namespace NpcForge.Core.Validation;

/// <summary>
/// Mensagens de validação em português ({0} = nome do campo, vindo de [Display]).
/// Sem elas, o .NET responde em inglês ("The field Str must be between 1 and 30.").
/// </summary>
public static class Messages
{
    public const string Required = "{0} é obrigatório.";
    public const string MaxLength = "{0} pode ter no máximo {1} caracteres.";
    public const string MaxItems = "{0} pode ter no máximo {1} itens.";
    public const string Range = "{0} deve estar entre {1} e {2}.";
    public const string AtLeast = "{0} deve ser pelo menos {1}.";
}
