using System.Text.Json;
using NpcForge.Core.Ai;

namespace NpcForge.UnitTests.Ai;

public class PromptBuilderTests
{
    private readonly PromptBuilder _builder = new();

    [Fact]
    public void BuildGenerateNpc_IncludesPromptSettingAndDelimiters()
    {
        var prompt = _builder.BuildGenerateNpc("anão ferreiro rabugento", "terror gótico em Barovia", false, null);

        Assert.Contains("<<<\nanão ferreiro rabugento\n>>>", prompt.User.ReplaceLineEndings("\n"));
        Assert.Contains("terror gótico em Barovia", prompt.User);
        Assert.Contains("JSON", prompt.System);
        Assert.Contains("português", prompt.System);
    }

    [Fact]
    public void SystemPrompt_AsksForPortugueseValuesInEnglishNamedFields()
    {
        // No teste real, o modelo respondeu "Humanoid", "Neutral" e "1d8 + 3 Con bonus":
        // os nomes dos campos em inglês o levavam a escrever os valores em inglês.
        var system = _builder.BuildGenerateNpc("x", null, true, null).System;

        Assert.Contains("\"humanoide (anão)\"", system);
        Assert.Contains("\"leal e neutro\"", system);
        Assert.Contains("\"1d8 + 3 Contundente\"", system);
        Assert.Contains("nomes dos campos", system);
    }

    [Fact]
    public void BuildGenerateNpc_WithoutSetting_SaysNotInformed()
        => Assert.Contains("não informado", _builder.BuildGenerateNpc("taverneira", null, false, null).User);

    [Fact]
    public void BuildGenerateNpc_WithStatBlockAndCr_RequiresStatBlockInSchemaAndMentionsCr()
    {
        var prompt = _builder.BuildGenerateNpc("lobisomem", null, true, "3");

        using var schema = JsonDocument.Parse(prompt.JsonSchema);
        var required = schema.RootElement.GetProperty("required").EnumerateArray().Select(e => e.GetString());
        Assert.Contains("statBlock", required);
        Assert.True(schema.RootElement.GetProperty("properties").TryGetProperty("statBlock", out _));
        Assert.Contains("Nível de Desafio 3", prompt.User);
    }

    [Fact]
    public void BuildGenerateNpc_WithoutStatBlock_SchemaHasNoStatBlock()
    {
        var prompt = _builder.BuildGenerateNpc("taverneira", null, false, null);

        using var schema = JsonDocument.Parse(prompt.JsonSchema);
        Assert.False(schema.RootElement.GetProperty("properties").TryGetProperty("statBlock", out _));
        Assert.Contains("Não inclua ficha", prompt.User);
    }

    [Fact]
    public void BuildRetry_AppendsParseError()
    {
        var original = _builder.BuildGenerateNpc("taverneira", null, false, null);

        var retry = _builder.BuildRetry(original, "JSON inválido: faltou fechar chave");

        Assert.StartsWith(original.User, retry.User);
        Assert.Contains("JSON inválido: faltou fechar chave", retry.User);
        Assert.Equal(original.System, retry.System);
        Assert.Equal(original.JsonSchema, retry.JsonSchema);
    }
}
