using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NpcForge.Api.Validation;

/// <summary>
/// Resposta 400 de validação, com os erros de leitura do JSON traduzidos.
/// Quando um valor tem o tipo errado (ex.: "size": "Enorme"), o System.Text.Json para e o ASP.NET
/// devolve a chave "$.npcs[0].statBlock.size" com a mensagem em inglês, mais um "The file field is required."
/// porque o corpo não pôde ser lido. Aqui a chave perde o "$.", a mensagem vira português e o erro do corpo some.
/// </summary>
public static partial class InvalidJsonResponses
{
    public static IActionResult Create(ActionContext context)
    {
        var modelState = context.ModelState;
        var hasJsonError = modelState.Keys.Any(k => k.StartsWith('$'));
        if (hasJsonError)
        {
            var parameters = context.ActionDescriptor.Parameters.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var translated = new ModelStateDictionary();
            foreach (var (key, entry) in modelState)
            {
                if (entry.Errors.Count == 0 || parameters.Contains(key)) continue;
                if (key.StartsWith('$'))
                    translated.AddModelError(key.TrimStart('$').TrimStart('.'), MessageFor(key));
                else
                    foreach (var error in entry.Errors) translated.AddModelError(key, error.ErrorMessage);
            }
            modelState = translated;
        }

        var problems = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var details = problems.CreateValidationProblemDetails(context.HttpContext, modelState);
        return new BadRequestObjectResult(details) { ContentTypes = { "application/problem+json" } };
    }

    // "$" sozinho (ou "$." sem campo) = o JSON nem chegou a ser lido. Senão, o último campo do caminho, sem índices.
    private static string MessageFor(string key)
    {
        var field = Index().Replace(key, "").Split('.').LastOrDefault(s => s.Length > 0 && s != "$");
        return field is null ? "O conteúdo não é um JSON válido." : $"O campo \"{field}\" tem um valor inválido.";
    }

    [GeneratedRegex(@"\[\d+\]")]
    private static partial Regex Index();
}
