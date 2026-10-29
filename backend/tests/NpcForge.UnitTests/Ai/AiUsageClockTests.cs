using NpcForge.Core.Ai;

namespace NpcForge.UnitTests.Ai;

public class AiUsageClockTests
{
    [Fact]
    public void Today_UsesUtcDate()
    {
        // 23:30 em Brasília (UTC−3) já é o dia seguinte em UTC.
        var now = new DateTimeOffset(2026, 10, 29, 23, 30, 0, TimeSpan.FromHours(-3));

        Assert.Equal(new DateOnly(2026, 10, 30), AiUsageClock.Today(now));
    }

    [Fact]
    public void ResetsAt_IsNextUtcMidnight()
    {
        var now = new DateTimeOffset(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);

        var resetsAt = AiUsageClock.ResetsAt(now);

        Assert.Equal(new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc), resetsAt);
        Assert.Equal(DateTimeKind.Utc, resetsAt.Kind);
    }
}
