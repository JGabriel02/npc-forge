using NpcForge.Core.Rules;

namespace NpcForge.UnitTests.Rules;

public class DndRulesTests
{
    [Theory]
    [InlineData(1, -5)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(12, 1)]
    [InlineData(20, 5)]
    [InlineData(30, 10)]
    public void AbilityModifier_FollowsFloorRule(int score, int expected)
        => Assert.Equal(expected, DndRules.AbilityModifier(score));

    [Theory]
    [InlineData("0", 2)]
    [InlineData("1/8", 2)]
    [InlineData("4", 2)]
    [InlineData("5", 3)]
    [InlineData("8", 3)]
    [InlineData("9", 4)]
    [InlineData("13", 5)]
    [InlineData("17", 6)]
    [InlineData("21", 7)]
    [InlineData("25", 8)]
    [InlineData("29", 9)]
    [InlineData("30", 9)]
    public void ProficiencyBonus_ByChallengeRating(string cr, int expected)
        => Assert.Equal(expected, DndRules.ProficiencyBonus(cr));

    [Fact]
    public void ProficiencyBonus_InvalidChallengeRating_Throws()
        => Assert.Throws<ArgumentException>(() => DndRules.ProficiencyBonus("1/3"));

    [Theory]
    [InlineData("0")]
    [InlineData("1/8")]
    [InlineData("1/4")]
    [InlineData("1/2")]
    [InlineData("1")]
    [InlineData("30")]
    public void IsValidChallengeRating_AcceptsOfficialValues(string cr)
        => Assert.True(DndRules.IsValidChallengeRating(cr));

    [Theory]
    [InlineData("31")]
    [InlineData("1/3")]
    [InlineData("-1")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0.5")]
    [InlineData(null)]
    public void IsValidChallengeRating_RejectsOtherValues(string? cr)
        => Assert.False(DndRules.IsValidChallengeRating(cr));

    [Fact]
    public void ChallengeRatings_ListsAllOfficialValuesInOrder()
    {
        Assert.Equal(34, DndRules.ChallengeRatings.Count);
        Assert.Equal(["0", "1/8", "1/4", "1/2", "1"], DndRules.ChallengeRatings.Take(5));
        Assert.Equal("30", DndRules.ChallengeRatings[^1]);
    }

    [Theory]
    [InlineData("4d8+4")]
    [InlineData("1d6")]
    [InlineData("10d10-5")]
    [InlineData("2d8 + 2")]   // formato das fichas do Livro do Jogador 2024
    [InlineData("1d4 – 1")]   // travessão (en dash), como no livro
    [InlineData("1d4 − 1")]   // sinal de menos tipográfico
    [InlineData(" 3d8 ")]
    [InlineData("2D8+2")]
    public void IsValidHitDice_AcceptsDiceNotation(string hitDice)
        => Assert.True(DndRules.IsValidHitDice(hitDice));

    [Theory]
    [InlineData("d8")]
    [InlineData("4d")]
    [InlineData("4d8+")]
    [InlineData("2d8 + ")]
    [InlineData("2d8 ++ 2")]
    [InlineData("2d8 * 2")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValidHitDice_RejectsInvalidNotation(string? hitDice)
        => Assert.False(DndRules.IsValidHitDice(hitDice));

    [Theory]
    [InlineData("2d8+2", "2d8 + 2")]
    [InlineData("2d8 + 2", "2d8 + 2")]
    [InlineData("1d4-1", "1d4 – 1")]
    [InlineData("1d4 – 1", "1d4 – 1")]
    [InlineData("1d4 − 1", "1d4 – 1")]
    [InlineData("3d8", "3d8")]
    [InlineData(" 2D8 +2 ", "2d8 + 2")]
    [InlineData("2d8 + 0", "2d8")]
    public void NormalizeHitDice_UsesThePlayersHandbookFormat(string input, string expected)
        => Assert.Equal(expected, DndRules.NormalizeHitDice(input));

    [Theory]
    [InlineData("2d8 + 2", 11)]
    [InlineData("4d8+4", 22)]
    [InlineData("1d4 – 1", 1)]
    [InlineData("1d4 - 5", 1)]
    [InlineData("18d8 + 54", 135)]
    public void AverageHitPoints_FollowsTheBookAverage(string hitDice, int expected)
        => Assert.Equal(expected, DndRules.AverageHitPoints(hitDice));

    [Fact]
    public void AverageHitPoints_InvalidNotation_Throws()
        => Assert.Throws<ArgumentException>(() => DndRules.AverageHitPoints("4d"));

    [Theory]
    [InlineData(0.3, "1/4")]
    [InlineData(0.4, "1/2")]
    [InlineData(7.4, "7")]
    [InlineData(7.5, "7")]
    [InlineData(31, "30")]
    [InlineData(-2, "0")]
    public void NearestChallengeRating_PicksClosestOfficialValue(double value, string expected)
        => Assert.Equal(expected, DndRules.NearestChallengeRating(value));

    [Theory]
    [InlineData("1/8", 0.125)]
    [InlineData("1/2", 0.5)]
    [InlineData("12", 12)]
    public void ChallengeRatingValue_ConvertsFractions(string cr, double expected)
        => Assert.Equal(expected, DndRules.ChallengeRatingValue(cr));

    [Fact]
    public void NormalizeHitDice_InvalidNotation_Throws()
        => Assert.Throws<ArgumentException>(() => DndRules.NormalizeHitDice("4d"));
}
