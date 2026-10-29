using NpcForge.Core.Npcs;

namespace NpcForge.UnitTests.Npcs;

public class FileNamesTests
{
    [Theory]
    [InlineData("Rahadin", "rahadin")]
    [InlineData("Ireena Kolyana!", "ireena-kolyana")]
    [InlineData("Ñandú", "nandu")]
    [InlineData("!!!", "")]
    public void Slug_RemovesAccentsAndSymbols(string name, string expected)
        => Assert.Equal(expected, FileNames.Slug(name));

    [Fact]
    public void Slug_IsLimitedTo50CharactersWithoutTrailingDash()
    {
        var slug = FileNames.Slug(new string('a', 49) + " b");

        Assert.True(slug.Length <= 50);
        Assert.False(slug.EndsWith('-'));
    }

    [Theory]
    [InlineData("Rahadin", "npc-rahadin.json")]
    [InlineData("!!!", "npc.json")]
    public void ForNpc_UsesSlugOrFallback(string name, string expected)
        => Assert.Equal(expected, FileNames.ForNpc(name));

    [Fact]
    public void ForList_UsesDate()
        => Assert.Equal("npcs-2026-10-29.json", FileNames.ForList(new DateOnly(2026, 10, 29)));
}
