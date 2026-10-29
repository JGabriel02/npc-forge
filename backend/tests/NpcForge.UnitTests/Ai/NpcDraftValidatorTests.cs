using NpcForge.Core.Ai;
using NpcForge.Core.Entities;

namespace NpcForge.UnitTests.Ai;

public class NpcDraftValidatorTests
{
    private static DraftResult Parse(string json, bool withStatBlock = true, string? requestedCr = null)
        => NpcDraftValidator.Parse(json, withStatBlock, requestedCr);

    [Fact]
    public void Parse_ValidJson_ReturnsDraftWithoutWarnings()
    {
        var result = Parse(TestDrafts.ValidJson);

        Assert.Null(result.ParseError);
        Assert.Empty(result.Warnings);
        Assert.Equal("Borin Martelo-de-Pedra", result.Draft!.Name);
        Assert.Equal(16, result.Draft.StatBlock!.Str);
        Assert.Equal(5, result.Draft.StatBlock.Actions![0].AttackBonus);
        Assert.Equal(CreatureSize.Medium, result.Draft.StatBlock.Size);
    }

    [Fact]
    public void Parse_JsonInsideCodeFenceAndProse_IsExtracted()
    {
        var raw = $"Claro! Aqui está o NPC:\n```json\n{TestDrafts.ValidJson}\n```\nEspero que goste.";

        var result = Parse(raw);

        Assert.Null(result.ParseError);
        Assert.Equal("anão", result.Draft!.Race);
    }

    [Fact]
    public void Parse_NumbersAsStrings_AreConverted()
    {
        var json = TestDrafts.With(r => { r.StatBlock()["str"] = "16"; r.StatBlock()["armorClass"] = "15"; });

        var result = Parse(json);

        Assert.Equal(16, result.Draft!.StatBlock!.Str);
        Assert.Equal(15, result.Draft.StatBlock.ArmorClass);
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("{ \"name\": \"Borin\", ")]
    public void Parse_InvalidJson_ReturnsParseError(string raw)
    {
        var result = Parse(raw);

        Assert.NotNull(result.ParseError);
        Assert.Null(result.Draft);
    }

    [Fact]
    public void Parse_MissingName_ReturnsParseError()
        => Assert.NotNull(Parse(TestDrafts.With(r => r.Remove("name"))).ParseError);

    [Fact]
    public void Parse_WithStatBlockButNoneReturned_ReturnsParseError()
        => Assert.NotNull(Parse(TestDrafts.With(r => r.Remove("statBlock"))).ParseError);

    [Fact]
    public void Parse_NotWithStatBlock_DropsStatBlockSilently()
    {
        var result = Parse(TestDrafts.ValidJson, withStatBlock: false);

        Assert.Null(result.Draft!.StatBlock);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Parse_AbilityAbove30_IsClampedWithWarning()
    {
        var result = Parse(TestDrafts.With(r => r.StatBlock()["str"] = 35));

        Assert.Equal(30, result.Draft!.StatBlock!.Str);
        Assert.Contains(result.Warnings, w => w.Contains("Força") && w.Contains("35") && w.Contains("30"));
    }

    [Fact]
    public void Parse_ArmorClassOutOfRange_IsClampedWithWarning()
    {
        var result = Parse(TestDrafts.With(r => r.StatBlock()["armorClass"] = 0));

        Assert.Equal(1, result.Draft!.StatBlock!.ArmorClass);
        Assert.Contains(result.Warnings, w => w.Contains("Classe de Armadura"));
    }

    [Fact]
    public void Parse_InvalidChallengeRating_UsesNearestWithWarning()
    {
        var result = Parse(TestDrafts.With(r => r.StatBlock()["challengeRating"] = "7.4"));

        Assert.Equal("7", result.Draft!.StatBlock!.ChallengeRating);
        Assert.Contains(result.Warnings, w => w.Contains("Nível de desafio"));
    }

    [Fact]
    public void Parse_RequestedChallengeRatingWins()
    {
        var result = Parse(TestDrafts.With(r => r.StatBlock()["challengeRating"] = "5"), requestedCr: "2");

        Assert.Equal("2", result.Draft!.StatBlock!.ChallengeRating);
        Assert.Contains(result.Warnings, w => w.Contains("Nível de desafio"));
    }

    [Fact]
    public void Parse_RequestedChallengeRatingMatching_HasNoWarning()
        => Assert.Empty(Parse(TestDrafts.ValidJson, requestedCr: "1").Warnings);

    [Fact]
    public void Parse_ZeroHitPoints_RecalculatedFromHitDice()
    {
        var result = Parse(TestDrafts.With(r => { r.StatBlock()["hitPoints"] = 0; r.StatBlock()["hitDice"] = "2d8 + 2"; }));

        Assert.Equal(11, result.Draft!.StatBlock!.HitPoints);
        Assert.Contains(result.Warnings, w => w.Contains("Pontos de vida"));
    }

    [Fact]
    public void Parse_InvalidHitDice_ClearedWithWarning()
    {
        var result = Parse(TestDrafts.With(r => r.StatBlock()["hitDice"] = "quatro dados"));

        Assert.Equal("", result.Draft!.StatBlock!.HitDice);
        Assert.Contains(result.Warnings, w => w.Contains("Dados de vida"));
    }

    [Fact]
    public void Parse_HitDiceIsNormalizedToBookFormat()
        => Assert.Equal("4d8 + 4", Parse(TestDrafts.With(r => r.StatBlock()["hitDice"] = "4d8+4")).Draft!.StatBlock!.HitDice);

    [Theory]
    [InlineData("Grande", CreatureSize.Large)]
    [InlineData("large", CreatureSize.Large)]
    [InlineData("Miúdo", CreatureSize.Tiny)]
    public void Parse_SizeInEnglishOrPortuguese_IsAccepted(string size, CreatureSize expected)
        => Assert.Equal(expected, Parse(TestDrafts.With(r => r.StatBlock()["size"] = size)).Draft!.StatBlock!.Size);

    [Fact]
    public void Parse_UnknownSize_DefaultsToMediumWithWarning()
    {
        var result = Parse(TestDrafts.With(r => r.StatBlock()["size"] = "colossal"));

        Assert.Equal(CreatureSize.Medium, result.Draft!.StatBlock!.Size);
        Assert.Contains(result.Warnings, w => w.Contains("Tamanho"));
    }

    [Fact]
    public void Parse_TooLongBackground_IsTruncatedWithWarning()
    {
        var result = Parse(TestDrafts.With(r => r["background"] = new string('a', 5000)));

        Assert.Equal(4000, result.Draft!.Background!.Length);
        Assert.Contains(result.Warnings, w => w.Contains("Histórico"));
    }

    [Fact]
    public void Parse_ItemsWithoutName_AreDroppedWithWarning()
    {
        var result = Parse(TestDrafts.With(r => r.StatBlock()["actions"] = System.Text.Json.Nodes.JsonNode.Parse(
            """[{ "name": "", "description": "sem nome" }, { "name": "Mordida", "description": "Ataque." }]""")));

        Assert.Equal(["Mordida"], result.Draft!.StatBlock!.Actions!.Select(a => a.Name));
        Assert.Contains(result.Warnings, w => w.Contains("Ações"));
    }

    [Fact]
    public void Parse_HugeHitDice_IsClearedInsteadOfThrowing()
    {
        // "99999999999" não cabe em int: antes, int.Parse estourava e a API respondia 500.
        var result = Parse(TestDrafts.With(r => r.StatBlock()["hitDice"] = "99999999999d8"));

        Assert.Equal("", result.Draft!.StatBlock!.HitDice);
        Assert.Contains(result.Warnings, w => w.Contains("Dados de vida"));
    }

    [Fact]
    public void Parse_NonFiniteNumber_IsTreatedAsMissing()
    {
        var json = TestDrafts.ValidJson.Replace("\"str\": 16", "\"str\": 1e400");

        var result = Parse(json);

        Assert.Equal(10, result.Draft!.StatBlock!.Str);
        Assert.Contains(result.Warnings, w => w.Contains("Força"));
    }

    [Fact]
    public void Parse_BlankOptionalTexts_BecomeNull()
    {
        var result = Parse(TestDrafts.With(r => { r["race"] = "  "; r.StatBlock()["skills"] = ""; }));

        Assert.Null(result.Draft!.Race);
        Assert.Null(result.Draft.StatBlock!.Skills);
    }
}
