using System;
using Server;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A ground a fighter found empty stays off its list for a while, for that fighter only.</summary>
public class DryGroundsTests
{
    private const uint Fighter = 0x7F001;
    private const uint OtherFighter = 0x7F002;
    private static readonly Point2D Ground = new(1849, 2915);
    private static readonly DateTime Start = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Note_DriesTheGroundForItsFighter_UntilTheSpawnIsBack()
    {
        DryGrounds.Note(Fighter, Ground, Start);

        Assert.True(DryGrounds.IsDry(Fighter, Ground, Start));
        Assert.False(DryGrounds.IsDry(OtherFighter, Ground, Start));
        Assert.True(DryGrounds.IsDry(Fighter, Ground, Start + DryGrounds.DryFor - TimeSpan.FromSeconds(1)));
        Assert.False(DryGrounds.IsDry(Fighter, Ground, Start + DryGrounds.DryFor));
    }
}
