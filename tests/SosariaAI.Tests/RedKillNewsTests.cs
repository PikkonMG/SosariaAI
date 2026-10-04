using System;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A red put down is told as a red kill naming everyone who did it, never as a murder.</summary>
public class RedKillNewsTests
{
    private const int Seed = 3;
    private const int NoRepeats = 0;
    private const double NoHeat = 0;

    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly ShardEvent RedDown = new()
    {
        At = Now,
        Type = ShardEventType.RedKill,
        Actor = "Grim",
        Other = "Halvard" + ShardEvent.NameSeparator + "Bryn",
        Place = "the Moonglow gate"
    };

    [Fact]
    public void DeathType_ARedIsNeverMurdered()
    {
        Assert.Equal(ShardEventType.Pk, ShardNews.DeathType(deadIsRed: false, killedByPlayerOrPk: true));
        Assert.Equal(ShardEventType.Death, ShardNews.DeathType(deadIsRed: true, killedByPlayerOrPk: true));
        Assert.Equal(ShardEventType.Death, ShardNews.DeathType(deadIsRed: false, killedByPlayerOrPk: false));
    }

    [Fact]
    public void Tell_NamesEveryKillerAndTheGate()
    {
        var heard = GossipLines.Tell(RedDown, "Kara", GossipRules.VividDetail, NoRepeats, Now, Seed);
        var fact = GossipLines.Fact(RedDown, "Kara", Now);

        Assert.Contains("grim", heard, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Halvard, Bryn killed the murderer Grim at the moonglow gate", fact);
    }

    [Fact]
    public void Tell_AKillerTellsItAndTheRedKeepsQuiet()
    {
        Assert.NotNull(GossipLines.Tell(RedDown, "Bryn", GossipRules.VividDetail, NoRepeats, Now, Seed));
        Assert.Null(GossipLines.Tell(RedDown, "Grim", GossipRules.VividDetail, NoRepeats, Now, Seed));
        Assert.True(GossipRules.KeepsQuiet(ShardEventType.RedKill, tellerIsActor: true));
        Assert.False(GossipRules.KeepsQuiet(ShardEventType.RedKill, tellerIsActor: false));
        Assert.NotNull(GossipLines.Reply(RedDown, Seed));
    }

    [Fact]
    public void RedKill_IsNoDanger()
    {
        Assert.Equal(NoHeat, DangerMap.HeatOf(ShardEventType.RedKill));
    }
}
