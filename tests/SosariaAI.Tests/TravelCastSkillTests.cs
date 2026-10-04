using System.Collections.Generic;
using Server;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// Norvel "fizzled recall" at the Den and his outing failed eleven seconds later: the second
/// cast came inside the recovery and would not begin. A cast that did not take waits for the
/// next one to begin, and only casts that began count as tries.
/// </summary>
public class TravelCastSkillTests
{
    private static uint _nextSerial = 0x8C01;

    public TravelCastSkillTests() => TestMap.EnsureInternal();

    [Fact]
    public void Tick_ARefusedRecastWaits_ThenCastsAgain()
    {
        var cast = new ScriptedCast(true, false, false, true);

        Assert.True(cast.Begin(Caster()));
        Assert.Equal(SkillStatus.Running, cast.Tick());
        Assert.Equal(SkillStatus.Running, cast.Tick());
        Assert.Equal(SkillStatus.Running, cast.Tick());
        Assert.Equal(0, cast.AnswersLeft);
        Assert.Null(cast.FailReason);
    }

    [Fact]
    public void Tick_EveryTrySpent_FailsWithTheReason()
    {
        var answers = new bool[RecallRules.MaxCastTries];

        for (var i = 0; i < answers.Length; i++)
        {
            answers[i] = true;
        }

        var cast = new ScriptedCast(answers);

        Assert.True(cast.Begin(Caster()));

        for (var i = 1; i < RecallRules.MaxCastTries; i++)
        {
            Assert.Equal(SkillStatus.Running, cast.Tick());
        }

        Assert.Equal(SkillStatus.Failed, cast.Tick());
        Assert.Equal(RecallRules.CastsSpentWhy, cast.FailReason);
    }

    private static SosariaCharacter Caster() => new((Serial)_nextSerial++);

    /// <summary>A travel cast whose casts begin or not as scripted; none of them takes.</summary>
    private sealed class ScriptedCast(params bool[] answers) : TravelCastSkill
    {
        private readonly Queue<bool> _answers = new(answers);

        public int AnswersLeft => _answers.Count;

        public override string Name => RecallRules.Kind;

        protected override bool BeginCast(SosariaCharacter character) => _answers.Dequeue();
    }
}
