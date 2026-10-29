using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using NpcForge.Core.Ai;

namespace NpcForge.Infrastructure.Ai;

/// <summary>
/// Provedor de IA local via Ollama (POST /api/chat). O "format" recebe o JSON schema,
/// o que faz o modelo responder no formato certo na grande maioria das vezes.
/// </summary>
public class OllamaProvider(HttpClient http, IOptions<AiOptions> options) : IAiProvider
{
    private readonly string _model = options.Value.Ollama.Model;

    public async Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, string jsonSchema, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = _model,
            ["stream"] = false,
            // Mantém o modelo carregado na GPU entre gerações (o padrão do Ollama é 5 min);
            // carregá-lo de novo custa dezenas de segundos.
            ["keep_alive"] = "30m",
            ["format"] = JsonNode.Parse(jsonSchema),
            ["options"] = new JsonObject { ["temperature"] = 0.8 },
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = systemPrompt },
                new JsonObject { ["role"] = "user", ["content"] = userPrompt },
            },
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("/api/chat", body, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException("A IA não está disponível agora.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                if (response.StatusCode == HttpStatusCode.NotFound && error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    throw new AiProviderException($"O modelo \"{_model}\" não está instalado no Ollama. Rode: ollama pull {_model}");
                throw new AiProviderException("A IA não está disponível agora.");
            }

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return json.RootElement.TryGetProperty("message", out var message)
                   && message.TryGetProperty("content", out var content)
                   && content.GetString() is { } text
                ? text
                : throw new AiProviderException("A IA respondeu em um formato inesperado.");
        }
    }
}
