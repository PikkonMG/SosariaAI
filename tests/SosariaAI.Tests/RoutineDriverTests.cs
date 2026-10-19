using System;
using Server;
using Server.Mobiles;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class RoutineDriverTests
{
    static RoutineDriverTests() => Timer.Init(0);

    public RoutineDriverTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
    }

    [Fact]
    public void OnAggressiveAction_WorkerHitByAPerson_FightsBackAtOnce()
    {
        // Flee against another person skipped every think. The target stood in
        // Britain until it died, then looted and died again.
        var victim = Worker((Serial)0x7501);
        var aggressor = StrongAttacker((Serial)0x7502);
        RoutineDriver.OnAggressiveAction(victim, aggressor);
        Assert.Equal(CharacterAction.Combat, victim.Motor.Action);
        Assert.False(victim.CheckFlee());
        Assert.True(victim.Warmode);
        Assert.Same(aggressor, victim.Combatant);
    }

    [Fact]
    public void OnAggressiveAction_ThirdHit_KeepsFighting()
    {
        // Once the character stands the fight, more hits from the same attacker must
        // not flip it back into a new flee every swing.
        var victim = Worker((Serial)0x7511);
        var aggressor = StrongAttacker((Serial)0x7512);
        RoutineDriver.OnAggressiveAction(victim, aggressor);
        RoutineDriver.OnAggressiveAction(victim, aggressor);
        RoutineDriver.OnAggressiveAction(victim, aggressor);

        Assert.Equal(CharacterAction.Combat, victim.Motor.Action);
        Assert.False(victim.CheckFlee());
        Assert.True(victim.Warmode);
    }

    [Fact]
    public void OnAggressiveAction_UnarmedHitByAPerson_Runs()
    {
        var victim = Worker((Serial)0x7521);
        var aggressor = StrongAttacker((Serial)0x7522);
        victim.Backpack.Delete();
        victim.FindItemOnLayer(Layer.OneHanded)?.Delete();

        RoutineDriver.OnAggressiveAction(victim, aggressor);

        Assert.True(victim.CheckFlee());
        Assert.Same(aggressor, victim.Combatant);
    }

    [Fact]
    public void FightEnd_WithNoHuntRunning_RestoresTheTownStance()
    {
        // A hunt that ended mid-fight could not drop its stance (a live foe stood there). The
        // flag outlived the hunt, every later fight end kept it, and the archer re-took the
        // same skeleton for seven hours with its day never chosen again.
        var archer = Worker((Serial)0x7531);
        archer.EnterHuntStance();

        archer.Motor.Action = CharacterAction.Combat;
        archer.Motor.Action = CharacterAction.Wander;

        Assert.False(archer.IsHunting);
        Assert.Equal(FightMode.None, archer.FightMode);
    }

    [Fact]
    public void HurtLatelyBy_OnlyAFoeWhoseBlowLanded()
    {
        var victim = Worker((Serial)0x7541);
        var striker = StrongAttacker((Serial)0x7542);
        var bystander = StrongAttacker((Serial)0x7543);

        victim.RegisterDamage(1, striker);

        Assert.True(victim.HurtLatelyBy(striker));
        Assert.False(victim.HurtLatelyBy(bystander));
    }

    /// <summary>A fresh character carries the worker build, armed.</summary>
    private static SosariaCharacter Worker(Serial serial)
    {
        var character = new SosariaCharacter(serial);
        character.DefaultMobileInit();
        return TestArms.Arm(character);
    }

    /// <summary>Hits plus strength must rate above the threat that lets a worker flee.</summary>
    private static SosariaCharacter StrongAttacker(Serial serial)
    {
        var aggressor = new SosariaCharacter(serial);
        aggressor.DefaultMobileInit();
        aggressor.RawStr = 200;
        aggressor.Hits = 200;
        return aggressor;
    }
}
