using System;
using Server;
using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// The guard-line stand-down only cleared Combatant and Warmode. A PK's
/// FightMode.Closest (and a hunter's Evil) re-acquired the adjacent foe on the
/// next think, so two fighters at the line traded stand-down lines forever.
/// </summary>
public class GuardStandDownTests
{
    private static uint _nextSerial = 0x7701;

    static GuardStandDownTests() => Timer.Init(0);

    public GuardStandDownTests() => TestMap.EnsureInternal();

    [Fact]
    public void StandDown_DisarmsFighter()
    {
        var fighter = Fighter();
        var foe = Fighter();
        fighter.Combatant = foe;
        fighter.FocusMob = foe;

        fighter.StandDown();

        Assert.Null(fighter.Combatant);
        Assert.Null(fighter.FocusMob);
        Assert.False(fighter.Warmode);
        Assert.Equal(FightMode.None, fighter.FightMode);
    }

    [Fact]
    public void StandDown_LeavesFoeUntouched()
    {
        var fighter = Fighter();
        var foe = Fighter();
        fighter.Combatant = foe;
        foe.Combatant = fighter;

        fighter.StandDown();

        Assert.Equal(fighter, foe.Combatant);
        Assert.Equal(FightMode.Evil, foe.FightMode);
    }

    [Fact]
    public void StandDown_RemembersTheFoe()
    {
        var fighter = Fighter();
        var foe = Fighter();
        fighter.Combatant = foe;

        fighter.StandDown();

        Assert.Equal(foe.Serial, fighter.LastStandDownFoe);
        Assert.Equal(Core.Now, fighter.LastStandDownAt);
    }

    [Fact]
    public void JoinAgainst_SameFoeInsideGrace_DoesNotReengage()
    {
        var fighter = Fighter();
        var foe = Fighter();
        fighter.Combatant = foe;
        fighter.StandDown();
        fighter.LastStandDownAt = DateTime.UtcNow;

        fighter.JoinAgainst(foe);

        Assert.Null(fighter.Combatant);
        Assert.False(fighter.Warmode);
    }

    [Fact]
    public void JoinAgainst_DifferentFoeInsideGrace_StillEngages()
    {
        var fighter = Fighter();
        var old = Fighter();
        var next = Fighter();
        fighter.Combatant = old;
        fighter.StandDown();
        fighter.LastStandDownAt = DateTime.UtcNow;

        fighter.JoinAgainst(next);

        Assert.Equal(next, fighter.Combatant);
        Assert.True(fighter.Warmode);
    }

    [Fact]
    public void RunFrom_KeepsTheGuardLineGraceForTheFoeBefore()
    {
        var fighter = Fighter();
        var stoodDownFrom = Fighter();
        var ranFrom = Fighter();
        fighter.Combatant = stoodDownFrom;
        fighter.StandDown();
        var stoodDownAt = DateTime.UtcNow;
        fighter.LastStandDownAt = stoodDownAt;

        CombatBrain.RunFrom(fighter, ranFrom);

        Assert.Equal(stoodDownFrom.Serial, fighter.LastStandDownFoe);
        Assert.Equal(stoodDownAt, fighter.LastStandDownAt);
        Assert.Equal(ranFrom.Serial, fighter.LastRanFrom);
        Assert.Equal(Core.Now, fighter.LastRanFromAt);
    }

    [Fact]
    public void LeavesBe_BothTheFoeStoodDownFromAndTheOneRunFrom()
    {
        var fighter = Fighter();
        var stoodDownFrom = Fighter();
        var ranFrom = Fighter();
        fighter.LastStandDownFoe = stoodDownFrom.Serial;
        fighter.LastStandDownAt = DateTime.UtcNow;
        fighter.LastRanFrom = ranFrom.Serial;
        fighter.LastRanFromAt = DateTime.UtcNow;

        Assert.True(fighter.StoodDownFrom(stoodDownFrom));
        Assert.False(fighter.StoodDownFrom(ranFrom));
        Assert.True(fighter.RanFromLately(ranFrom));
        Assert.False(fighter.RanFromLately(stoodDownFrom));
        Assert.True(fighter.LeavesBe(stoodDownFrom));
        Assert.True(fighter.LeavesBe(ranFrom));
        Assert.False(fighter.LeavesBe(Fighter()));
    }

    [Fact]
    public void JoinAgainst_FoeRunFromInsideGrace_DoesNotReengage()
    {
        var fighter = Fighter();
        var foe = Fighter();
        fighter.LastRanFrom = foe.Serial;
        fighter.LastRanFromAt = DateTime.UtcNow;

        fighter.JoinAgainst(foe);

        Assert.Null(fighter.Combatant);
    }

    private static SosariaCharacter Fighter()
    {
        var character = new SosariaCharacter((Serial)_nextSerial++);
        character.DefaultMobileInit();
        character.FightMode = FightMode.Evil;
        character.Warmode = true;
        return character;
    }
}
