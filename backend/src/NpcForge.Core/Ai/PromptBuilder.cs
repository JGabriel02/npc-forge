using System.Text.Json.Nodes;

namespace NpcForge.Core.Ai;

public record AiPrompt(string System, string User, string JsonSchema);

/// <summary>Monta os prompts a partir dos templates em Ai/Prompts (recursos embutidos na DLL).</summary>
public class PromptBuilder
{
    private readonly string _system = Load("system.txt");
    private readonly string _generateNpc = Load("generate-npc.txt");
    private readonly string _retry = Load("retry.txt");
    private readonly string _npcSchema = Load("npc-draft.schema.json");
    private readonly string _statBlockSchema = Load("stat-block.schema.json");

    public AiPrompt BuildGenerateNpc(string userPrompt, string? campaignSetting, bool withStatBlock, string? challengeRating)
    {
        var statBlockInstruction = (withStatBlock, challengeRating) switch
        {
            (false, _) => "Não inclua ficha de combate: este NPC é apenas narrativo.",
            (true, null) => "Inclua uma ficha de combate completa (statBlock) com um Nível de Desafio adequado ao pedido.",
            (true, var cr) => $"Inclua uma ficha de combate completa (statBlock) com Nível de Desafio {cr}, " +
                              "com CA, PV, atributos e ataques coerentes com esse ND.",
        };

        var user = _generateNpc
            .Replace("{{prompt}}", userPrompt.Trim())
            .Replace("{{setting}}", string.IsNullOrWhiteSpace(campaignSetting) ? "não informado" : campaignSetting.Trim())
            .Replace("{{statBlockInstruction}}", statBlockInstruction);

        return new AiPrompt(_system, user, BuildSchema(withStatBlock));
    }

    public AiPrompt BuildRetry(AiPrompt original, string parseError)
        => original with { User = original.User + _retry.Replace("{{error}}", parseError) };

    private string BuildSchema(bool withStatBlock)
    {
        var schema = JsonNode.Parse(_npcSchema)!.AsObject();
        if (withStatBlock)
        {
            schema["properties"]!.AsObject()["statBlock"] = JsonNode.Parse(_statBlockSchema);
            schema["required"]!.AsArray().Add("statBlock");
        }
        return schema.ToJsonString();
    }

    private static string Load(string fileName)
    {
        var name = $"NpcForge.Core.Ai.Prompts.{fileName}";
        using var stream = typeof(PromptBuilder).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Recurso de prompt não encontrado: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
