namespace NpcForge.Core.Ai;

/// <summary>
/// Fala com um modelo de linguagem. Não sabe nada de RPG: recebe instruções, pedido e o formato JSON esperado,
/// e devolve o texto da resposta. Cada provedor (Ollama, nuvem...) é uma implementação.
/// </summary>
public interface IAiProvider
{
    /// <exception cref="AiProviderException">Modelo indisponível ou resposta de erro.</exception>
    Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken ct);
}

/// <summary>Falha do provedor de IA, com mensagem pronta para o usuário (em português).</summary>
public class AiProviderException(string message, Exception? inner = null) : Exception(message, inner);
