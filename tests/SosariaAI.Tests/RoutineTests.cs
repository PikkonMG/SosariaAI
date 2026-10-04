using System;
using System.Collections.Generic;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class RoutineTests
{
    [Fact]
    public void Tick_Done_SetsNeedsNextAndDoesNotBeginTheNextStep()
    {
        var first = new ScriptedSkill("first", [SkillStatus.Running, SkillStatus.Done]);
        var second = new ScriptedSkill("second", [SkillStatus.Running]);
        var routine = new Routine([first, second]);

        routine.Tick();
        routine.Tick();

        Assert.True(routine.NeedsNext);
        Assert.Same(first, routine.CurrentSkill);
        Assert.Equal(1, first.BeginCount);
        Assert.Equal(0, second.BeginCount);

        routine.Tick();
        Assert.Equal(0, second.BeginCount);
    }

    [Fact]
    public void Tick_SkipsFailedStepAfterPauseThenStartsNext()
    {
        var first = new ScriptedSkill("first", [SkillStatus.Failed]);
        var second = new ScriptedSkill("second", [SkillStatus.Running]);
        var routine = new Routine([first, second]);
        var ended = new List<SkillStatus>();
        routine.SkillEnded += (_, _, status) => ended.Add(status);

        routine.Tick();
        Assert.True(routine.NeedsNext);
        Assert.Equal(0, second.BeginCount);
        Assert.Equal([SkillStatus.Failed], ended);

        for (var i = 0; i < Routine.FailedPauseTicks; i++)
        {
            routine.Tick();
        }

        Assert.Equal(0, second.BeginCount);
        Assert.True(routine.NeedsNext);
    }

    [Fact]
    public void Tick_InstantDone_WaitsOneThinkNotAFreeze()
    {
        // A one-tick practice step once froze the character for twenty seconds, which read
        // as standing about. Practice now runs as a session; a step that still ends at once
        // waits a single think before the next choice.
        var instant = new ScriptedSkill("recall", [SkillStatus.Done]);
        var routine = new Routine([instant]);
        var ended = new List<SkillStatus>();
        routine.SkillEnded += (_, _, status) => ended.Add(status);

        routine.Tick();
        Assert.Equal([SkillStatus.Done], ended);
        Assert.False(routine.NeedsNext);

        routine.Tick();
        Assert.True(routine.NeedsNext);
        Assert.Equal(1, instant.BeginCount);
    }

    [Fact]
    public void Dwell_IsASingleThink()
    {
        Assert.Equal(1, RoutineDwell.HoldTicks(SkillStatus.Done, ticksRun: 1));
        Assert.Equal(0, RoutineDwell.HoldTicks(SkillStatus.Done, ticksRun: 2));
        Assert.Equal(0, RoutineDwell.HoldTicks(SkillStatus.Failed, ticksRun: 1));
    }

    [Fact]
    public void Tick_Done_DoesNotRestartTheList()
    {
        var first = new ScriptedSkill("first", [SkillStatus.Running, SkillStatus.Done]);
        var second = new ScriptedSkill("second", [SkillStatus.Done]);
        var routine = new Routine([first, second]);
        routine.Tick();
        routine.Tick();
        Assert.True(routine.NeedsNext);
        Assert.Same(first, routine.CurrentSkill);
        Assert.Equal(0, second.BeginCount);
    }

    [Fact]
    public void Constructor_ResumesAtSavedIndex()
    {
        var first = new ScriptedSkill("first", [SkillStatus.Running]);
        var second = new ScriptedSkill("second", [SkillStatus.Running]);
        var routine = new Routine([first, second], startIndex: 1);

        routine.Tick();

        Assert.Same(second, routine.CurrentSkill);
        Assert.Equal(0, first.BeginCount);
        Assert.Equal(1, second.BeginCount);
    }

    [Fact]
    public void Tick_RaisesStartedAndEndedEvents()
    {
        var skill = new ScriptedSkill("only", [SkillStatus.Running, SkillStatus.Done]);
        var routine = new Routine([skill, new ScriptedSkill("next", [SkillStatus.Running])]);
        var started = new List<string>();
        var ended = new List<(string Name, SkillStatus Status)>();
        routine.SkillStarted += (_, s) => started.Add(s.Name);
        routine.SkillEnded += (_, s, status) => ended.Add((s.Name, status));

        routine.Tick();
        Assert.Equal(["only"], started);
        Assert.Empty(ended);

        routine.Tick();
        Assert.Equal(["only"], started);
        Assert.Equal([("only", SkillStatus.Done)], ended);
    }

    [Fact]
    public void AbortActive_StopsCurrentSkillAndWaitsForTheNextChoice()
    {
        var skill = new ScriptedSkill("walk", [SkillStatus.Running, SkillStatus.Running]);
        var routine = new Routine([skill]);

        routine.Tick();
        routine.AbortActive();

        Assert.Equal(1, skill.AbortCount);
        Assert.True(routine.NeedsNext);

        routine.Tick();

        Assert.Equal(1, skill.BeginCount);
    }

    [Fact]
    public void FailActive_EndsTheSkillAsAFailureWithItsReason()
    {
        // The watchdog's plain abort left no mark, and three Minoc sellers began the same
        // stalled sale again the next second.
        const string why = FleetWatchdog.StalledWhy;
        var skill = new ScriptedSkill(SkillKinds.VendorSell, [SkillStatus.Running, SkillStatus.Running]);
        var routine = new Routine([skill]);
        var ended = new List<SkillStatus>();
        routine.SkillEnded += (_, _, status) => ended.Add(status);

        routine.Tick();
        routine.FailActive(why);

        Assert.Equal([SkillStatus.Failed], ended);
        Assert.Equal(why, skill.FailReason);
        Assert.Equal(1, skill.AbortCount);
        Assert.True(routine.NeedsNext);
        Assert.False(routine.WasAborted);
    }

    [Fact]
    public void FailActive_BeforeBegin_OnlyAborts()
    {
        var skill = new ScriptedSkill(SkillKinds.VendorSell, [SkillStatus.Running]);
        var routine = new Routine([skill]);
        var ended = new List<SkillStatus>();
        routine.SkillEnded += (_, _, status) => ended.Add(status);

        routine.FailActive(FleetWatchdog.StalledWhy);

        Assert.Empty(ended);
        Assert.True(routine.NeedsNext);
    }

    [Fact]
    public void ContinueAfterAbort_Idle_DoesNotBeginAgain()
    {
        // Five Abort + idle fallback Replace calls began IdleWander five times
        // in five seconds. Continue after abort keeps the same skill running.
        var ticks = new SkillStatus[8];
        Array.Fill(ticks, SkillStatus.Running);
        var skill = new ScriptedSkill(SkillKinds.IdleWander, ticks);
        var routine = new Routine([skill]);
        var started = 0;
        routine.SkillStarted += (_, _) => started++;

        routine.Tick();
        Assert.Equal(1, skill.BeginCount);
        Assert.Equal(1, started);

        for (var i = 0; i < 5; i++)
        {
            routine.AbortActive();
            Assert.True(routine.ContinueAfterAbort());
            routine.Tick();
        }

        Assert.Equal(1, skill.BeginCount);
        Assert.Equal(1, started);
        Assert.False(routine.NeedsNext);
    }

    [Fact]
    public void ContinueAfterAbort_AfterDone_DoesNotResume()
    {
        var skill = new ScriptedSkill(SkillKinds.IdleWander, [SkillStatus.Running, SkillStatus.Done]);
        var routine = new Routine([skill]);

        routine.Tick();
        routine.Tick();
        Assert.True(routine.NeedsNext);
        Assert.False(routine.ContinueAfterAbort());
        Assert.True(routine.NeedsNext);
    }

    [Fact]
    public void Tick_SixtyTimesWithOneSkill_BeginsOnce()
    {
        var ticks = new SkillStatus[60];

        for (var i = 0; i < ticks.Length; i++)
        {
            ticks[i] = SkillStatus.Running;
        }

        var skill = new ScriptedSkill("work", ticks);
        var routine = new Routine([skill]);

        for (var i = 0; i < 60; i++)
        {
            routine.Tick();
        }

        Assert.Equal(1, skill.BeginCount);
        Assert.False(routine.NeedsNext);
    }

    [Fact]
    public void Constructor_RejectsEmptySteps()
    {
        Assert.Throws<ArgumentException>(() => new Routine(Array.Empty<Skill>()));
    }

    [Fact]
    public void Replace_AbortsAndStartsAtZero()
    {
        var first = new ScriptedSkill("first", [SkillStatus.Running]);
        var next = new ScriptedSkill("next", [SkillStatus.Running]);
        var routine = new Routine([first]);
        routine.Tick();

        routine.Replace([next]);
        Assert.Same(next, routine.CurrentSkill);
        Assert.Equal(1, first.AbortCount);

        routine.Tick();
        Assert.Equal(1, next.BeginCount);
    }

    [Fact]
    public void Tick_SkillThatReplacesRoutineDuringBegin_BeginsNewStepNext()
    {
        var replacement = new ScriptedSkill("moved", [SkillStatus.Running]);
        Routine routine = null;
        var decider = new ReplacingSkill("decide", () => routine.Replace([replacement]));
        routine = new Routine([decider]);

        // First tick: decider.Begin replaces the routine. The stale decider must not tick.
        routine.Tick();
        Assert.Equal(0, decider.TickCount);
        Assert.Equal(0, replacement.BeginCount);

        // Second tick: the new step begins cleanly, no exception.
        routine.Tick();
        Assert.Equal(1, replacement.BeginCount);
        Assert.Same(replacement, routine.CurrentSkill);
    }

    [Fact]
    public void FailedWalk_DoesNotPreventNewWorkFromStartingItsOwnRoute()
    {
        var walk = new ScriptedSkill(SkillKinds.GoTo, [SkillStatus.Failed]);
        var mine = new ScriptedSkill(SkillKinds.Mine, [SkillStatus.Running, SkillStatus.Done]);
        var routine = new Routine([walk]);
        routine.Tick();

        routine.Replace([mine], keepFailPause: true);
        for (var i = 0; i < Routine.FailedPauseTicks; i++)
        {
            routine.Tick();
        }

        routine.Tick();
        Assert.Equal(1, mine.BeginCount);
        Assert.False(routine.NeedsNext);
        routine.Tick();
        Assert.True(routine.NeedsNext);
    }

    private sealed class ReplacingSkill : Skill
    {
        private readonly Action _onBegin;

        public ReplacingSkill(string name, Action onBegin)
        {
            Name = name;
            _onBegin = onBegin;
        }

        public override string Name { get; }

        public int TickCount { get; private set; }

        public override bool Begin(SosariaAI.Mobiles.SosariaCharacter character)
        {
            _onBegin();
            return true;
        }

        public override SkillStatus Tick()
        {
            TickCount++;
            return SkillStatus.Running;
        }

        public override void Abort()
        {
        }
    }

    [Fact]
    public void Resume_GivesHeldTimeToTheBegunSkill()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0);
        var skill = new ScriptedSkill("walk", [SkillStatus.Running]);
        var routine = new Routine([skill]);
        routine.Tick();

        routine.Pause(start);
        routine.Pause(start.AddSeconds(5));
        routine.Resume(start.AddSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), skill.Held);

        routine.Resume(start.AddMinutes(5));
        Assert.Equal(TimeSpan.FromSeconds(30), skill.Held);
    }

    [Fact]
    public void Pause_ForAFight_ResumesTheSameSkillWithoutANewBegin()
    {
        // A fight holds the walk home; it goes on afterwards instead of starting over.
        var start = new DateTime(2026, 9, 25, 9, 0, 0);
        var fight = TimeSpan.FromSeconds(20);
        var skill = new ScriptedSkill("GoHome", [SkillStatus.Running, SkillStatus.Running, SkillStatus.Done]);
        var routine = new Routine([skill]);
        var starts = 0;
        routine.SkillStarted += (_, _) => starts++;
        routine.Tick();

        routine.Pause(start);
        routine.Resume(start + fight);
        routine.Tick();

        Assert.False(routine.NeedsNext);
        Assert.Equal(1, skill.BeginCount);
        Assert.Equal(0, skill.AbortCount);
        Assert.Equal(1, starts);
        Assert.Equal(fight, skill.Held);
    }

    [Fact]
    public void Resume_BeforeBegin_DoesNotTouchTheSkill()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0);
        var skill = new ScriptedSkill("walk", [SkillStatus.Running]);
        var routine = new Routine([skill]);

        routine.Pause(start);
        routine.Resume(start.AddSeconds(30));

        Assert.Equal(TimeSpan.Zero, skill.Held);
    }

    private sealed class ScriptedSkill : Skill
    {
        private readonly Queue<SkillStatus> _ticks;

        public ScriptedSkill(string name, IEnumerable<SkillStatus> ticks)
        {
            Name = name;
            _ticks = new Queue<SkillStatus>(ticks);
        }

        public override string Name { get; }

        public int BeginCount { get; private set; }

        public int AbortCount { get; private set; }

        public TimeSpan Held { get; private set; }

        public override bool Begin(SosariaAI.Mobiles.SosariaCharacter character)
        {
            BeginCount++;
            return true;
        }

        public override SkillStatus Tick() => _ticks.Count == 0 ? SkillStatus.Running : _ticks.Dequeue();

        public override void Abort() => AbortCount++;

        public override void Resume(TimeSpan held) => Held += held;
    }
}
