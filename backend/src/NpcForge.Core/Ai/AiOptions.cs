using System.ComponentModel.DataAnnotations;

namespace NpcForge.Core.Ai;

/// <summary>Configuração da seção "Ai".</summary>
public class AiOptions
{
    public const string Section = "Ai";

    /// <summary>Feature flag: desligada, o app roda sem nenhuma funcionalidade de IA (versão web).</summary>
    public bool Enabled { get; set; }

    public string Provider { get; set; } = "Ollama";

    /// <summary>
    /// 120 s: com o modelo já carregado, uma geração com ficha leva ~35 s numa GPU de 16 GB;
    /// a primeira, que carrega o modelo, passou de 60 s no teste real.
    /// </summary>
    [Range(5, 600)]
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gerações por usuário por dia. 0 = sem limite (padrão: a IA roda no computador do próprio usuário,
    /// sem custo). Útil com um provedor pago na nuvem.
    /// </summary>
    [Range(0, 1000)]
    public int DailyGenerationLimit { get; set; }

    public OllamaOptions Ollama { get; set; } = new();
}

public class OllamaOptions
{
    [Required, Url]
    public string BaseUrl { get; set; } = "http://localhost:11434";

    [Required]
    public string Model { get; set; } = "qwen2.5:14b";
}
