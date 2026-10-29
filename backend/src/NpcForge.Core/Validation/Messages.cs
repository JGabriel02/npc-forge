namespace NpcForge.Core.Validation;

/// <summary>
/// Mensagens de validação em português ({0} = nome do campo, vindo de [Display]). "O campo" na frente
/// evita erros de concordância ("Descrição é obrigatório", "Pontos de vida deve").
/// Sem elas, o .NET responde em inglês ("The field Str must be between 1 and 30.").
/// </summary>
public static class Messages
{
    public const string Required = "O campo {0} é obrigatório.";
    public const string MaxLength = "O campo {0} pode ter no máximo {1} caracteres.";
    public const string MaxItems = "O campo {0} pode ter no máximo {1} itens.";
    public const string Range = "O campo {0} deve estar entre {1} e {2}.";
    public const string AtLeast = "O campo {0} deve ser pelo menos {1}.";
}
