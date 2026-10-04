using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A rally calls a neighbour into someone else's fight. With a hunt's abort, one blow on a
/// stranger in a dungeon ended every nearby crawler's whole trip.
/// </summary>
public class CalledFightTests
{
    private const double StopBelowHits = 0.5;
    private static readonly TimeSpan HuntLength = TimeSpan.FromMinutes(10);
    private static uint _nextSerial = 0x7801;

    static CalledFightTests() => Timer.Init(0);

    public CalledFightTests() => TestMap.EnsureInternal();

    [Fact]
    public void JoinAgainst_DungeonTrip_IsHeldNotEnded()
    {
        var crawler = Fighter();
        var routine = new Routine([new DungeonTripSkill(Point3D.Zero, Hunt(), null)]);
        TestRoutine.Give(crawler, routine);
        var foe = Fighter();

        crawler.JoinAgainst(foe);

        Assert.False(routine.NeedsNext);
        Assert.False(routine.WasAborted);
        Assert.Same(foe, crawler.Combatant);
    }

    [Fact]
    public void JoinAgainst_Hunt_EndsForTheCalledFight()
    {
        var hunter = Fighter();
        var routine = new Routine([Hunt()]);
        TestRoutine.Give(hunter, routine);

        hunter.JoinAgainst(Fighter());

        Assert.True(routine.NeedsNext);
    }

    private static HuntSkill Hunt() =>
        new(new Rectangle2D(0, 0, 1, 1), HuntLength, StopBelowHits, null, null);

    private static SosariaCharacter Fighter()
    {
        var character = new SosariaCharacter((Serial)_nextSerial++);
        character.DefaultMobileInit();
        return character;
    }
}
