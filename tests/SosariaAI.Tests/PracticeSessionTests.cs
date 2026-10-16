using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Tests;

public class PracticeSessionTests : IDisposable
{
    private const int OneBegin = 1;
    private const int TicksOfAThreeTickTry = 3;
    private const int NoAbort = 0;
    private const int OneAbort = 1;
    private static readonly Point3D Bank = new(100, 100, 0);

    private readonly List<IEntity> _placed = [];

    static PracticeSessionTests() => Timer.Init(0);

    public PracticeSessionTests()
    {
        TestMap.EnsureLand();
        TestMap.EnsureRunningWorld();
        TestMap.EnsureDecayScheduler();
    }

    public void Dispose() => TestMap.Remove(_placed);

    [Fact]
    public void MultiTickTry_RunsEveryTickUntilItEnds_AndBeginsOnce()
    {
        // A cast speaks its words on one tick and aims on a later one. Ticked once per rep
        // interval and begun again each time, it never reached its aim.
        var practice = new ScriptedSkill(SkillStatus.Running, SkillStatus.Running, SkillStatus.Done);
        var session = new PracticeSession(practice);

        Assert.True(session.Begin(InWorld((Serial)0x6A51)));

        for (var i = 0; i < TicksOfAThreeTickTry; i++)
        {
            Assert.Equal(SkillStatus.Running, session.Tick());
        }

        Assert.Equal(TicksOfAThreeTickTry, practice.Ticks);
        Assert.Equal(OneBegin, practice.Begins);
        Assert.Equal(NoAbort, practice.Aborts);
    }

    [Fact]
    public void Abort_StopsTheRunningTry()
    {
        var practice = new ScriptedSkill(SkillStatus.Running, SkillStatus.Done);
        var session = new PracticeSession(practice);

        Assert.True(session.Begin(InWorld((Serial)0x6A53)));
        session.Tick();
        session.Abort();

        Assert.Equal(OneAbort, practice.Aborts);
    }

    /// <summary>A person standing on the test land.</summary>
    private SosariaCharacter InWorld(Serial serial)
    {
        var character = new SosariaCharacter(serial);
        character.DefaultMobileInit();
        character.MoveToWorld(Bank, TestMap.EnsureLand());
        _placed.Add(character);
        return character;
    }

    /// <summary>
    /// A practice try that answers its ticks from a script, then stays done. It carries the
    /// meditation's name, a skill that stands still between tries.
    /// </summary>
    private sealed class ScriptedSkill(params SkillStatus[] script) : Skill
    {
        public SkillStatus[] Script { get; } = script;

        public int Begins { get; private set; }

        public int Ticks { get; private set; }

        public int Aborts { get; private set; }

        public override string Name => SkillKinds.Meditate;

        public override bool Begin(SosariaCharacter character)
        {
            Begins++;
            return true;
        }

        public override SkillStatus Tick()
        {
            var status = Ticks < Script.Length ? Script[Ticks] : SkillStatus.Done;
            Ticks++;
            return status;
        }

        public override void Abort() => Aborts++;
    }
}
