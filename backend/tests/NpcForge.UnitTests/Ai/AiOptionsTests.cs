using NpcForge.Core.Ai;

namespace NpcForge.UnitTests.Ai;

public class AiOptionsTests
{
    [Fact]
    public void DailyGenerationLimit_DefaultsToUnlimited()
        // A IA roda no computador do próprio usuário: sem custo, então sem limite por padrão.
        => Assert.Equal(0, new AiOptions().DailyGenerationLimit);
}
