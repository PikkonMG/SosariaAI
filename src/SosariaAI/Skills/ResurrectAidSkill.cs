using System;
using Server;
using Server.Items;
using Server.Spells;
using Server.Spells.Eighth;
using Server.Targeting;
using SosariaAI.Behaviour;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// Walks to a ghost and raises it the way a player does: the words of Resurrection and
/// the target cursor on the ghost, or a bandage used on it. A fizzle, a disturbed cast or
/// a slipped bandage is tried again a few times; hands busy with a cast, a cursor or a
/// bandage finish first. A foe on the helper or the killer back at the body ends the raise
/// (<see cref="ResurrectAid.Hindrance"/>). Every failure names its reason and the ghost. A
/// human ghost gets the engine's gump; a character ghost is answered by <see cref="ResurrectWatch"/>.
/// </summary>
public sealed class ResurrectAidSkill : Skill
{
    public const string SkillName = "ResurrectAid";

    /// <summary>
    /// Tries before the helper gives up. A healer at 80 raises one bandage in four and a mage
    /// at 80 one cast in four, so three tries left 69 ghosts in 150 minutes standing dead.
    /// Five fit well inside <see cref="GiveUpAfter"/> at a bandage's pace.
    /// </summary>
    public const int MaxAttempts = 5;
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(90);

    /// <summary>A pause after a try; it also lets <see cref="ResurrectWatch"/> read a bandage's outcome.</summary>
    public static readonly TimeSpan AttemptGap = TimeSpan.FromSeconds(2);

    private SosariaCharacter _helper;
    private DateTime _startedAt;
    private DateTime _nextAttemptAt;
    private AidMethod _inFlight;
    private int _attempts;
    private bool _offered;

    public ResurrectAidSkill(Mobile ghost) => Ghost = ghost;

    public Mobile Ghost { get; }

    public override string Name => SkillName;

    /// <summary>The ghost it walks to and raises.</summary>
    public override JobTarget? AimedAt => Ghost == null ? null : new JobTarget(JobTargetRest.KeyOf(Ghost), Point3D.Zero);

    public override bool Begin(SosariaCharacter character)
    {
        _helper = character;
        _startedAt = Core.Now;
        _nextAttemptAt = default;
        _inFlight = AidMethod.None;
        _attempts = 0;
        _offered = false;

        if (character is not { Deleted: false, Alive: true })
        {
            return CannotStart(ResurrectAid.FailLine(ResurrectAid.HelperDownWhy, Ghost?.Name));
        }

        if (Ghost is not { Deleted: false, Alive: false })
        {
            return CannotStart(ResurrectAid.FailLine(ResurrectAid.GhostGoneWhy, Ghost?.Name));
        }

        return ResurrectOffer.MethodFor(character) != AidMethod.None ||
               CannotStart(ResurrectAid.FailLine(ResurrectAid.NoMeansWhy, Ghost.Name));
    }

    public override SkillStatus Tick()
    {
        if (_helper is not { Deleted: false, Alive: true })
        {
            return GiveUp(ResurrectAid.HelperDownWhy);
        }

        if (Ghost == null || Ghost.Deleted)
        {
            return GiveUp(ResurrectAid.GhostGoneWhy);
        }

        if (Ghost.Alive)
        {
            Talk.Say(_helper, TalkCategory.ResWelcome);
            return Finish(SkillStatus.Done);
        }

        if (Ghost.Map != _helper.Map)
        {
            return GiveUp(ResurrectAid.OtherMapWhy);
        }

        if (Core.Now - _startedAt >= GiveUpAfter)
        {
            return GiveUp(ResurrectAid.TimeUpWhy);
        }

        if (ResurrectOffer.Hindrance(_helper, Ghost) is { } hindrance)
        {
            return GiveUp(hindrance);
        }

        if (_inFlight != AidMethod.None)
        {
            return TickAttempt();
        }

        if (!_helper.InRange(Ghost, ResurrectAid.CastRange))
        {
            return _helper.Motor.MoveTo(Ghost, ResurrectAid.CastRange) ? SkillStatus.Running : GiveUp(ResurrectAid.NoWalkWhy);
        }

        _helper.Motor.ClearMoveIntent();
        _helper.Direction = _helper.GetDirectionTo(Ghost);

        if (!_offered)
        {
            _offered = true;
            Talk.Say(_helper, TalkCategory.ResOffer);
        }

        if (Core.Now < _nextAttemptAt)
        {
            return SkillStatus.Running;
        }

        if (_attempts >= MaxAttempts)
        {
            return GiveUp(ResurrectAid.TriesSpentWhy(_attempts));
        }

        var method = ResurrectOffer.MethodFor(_helper);

        if (method == AidMethod.None)
        {
            return GiveUp(ResurrectAid.NoMeansWhy);
        }

        return HandsBusy(method) ? SkillStatus.Running : StartAttempt(method);
    }

    public override void Abort()
    {
        Finish(SkillStatus.Failed);
        _helper = null;
    }

    public override void Resume(TimeSpan held)
    {
        _startedAt = SkillClock.Shift(_startedAt, held);
        _nextAttemptAt = SkillClock.Shift(_nextAttemptAt, held);
    }

    private SkillStatus StartAttempt(AidMethod method)
    {
        if (method == AidMethod.Spell)
        {
            if (!SpellCasting.TryBegin(_helper, new ResurrectionSpell(_helper)))
            {
                return GiveUp(ResurrectAid.WordsRefusedWhy);
            }
        }
        else
        {
            _helper.Backpack.FindItemByType<Bandage>().OnDoubleClick(_helper);
            AnswerCursor();
        }

        _inFlight = method;
        _attempts++;
        return SkillStatus.Running;
    }

    private bool HandsBusy(AidMethod method) =>
        ResurrectAid.HandsBusy(
            _helper.Spell != null,
            _helper.Target != null,
            BandageContext.GetContext(_helper) != null,
            Core.TickCount - _helper.NextSpellTime < 0,
            method
        );

    /// <summary>
    /// A spell answers its cursor once the words are done and ends when the cast does. A
    /// bandage runs until its timer ends. The ghost rising ends the skill on the next tick.
    /// </summary>
    private SkillStatus TickAttempt()
    {
        var spell = _inFlight == AidMethod.Spell;
        var busy = spell ? _helper.Spell != null : BandageContext.GetContext(_helper)?.Patient == Ghost;

        if (busy)
        {
            if (spell && _helper.Target is SpellTarget<Mobile> { Spell: ResurrectionSpell })
            {
                AnswerCursor();
            }

            _helper.Motor.ClearMoveIntent();
            return SkillStatus.Running;
        }

        _inFlight = AidMethod.None;
        _nextAttemptAt = Core.Now + AttemptGap;
        return SkillStatus.Running;
    }

    private void AnswerCursor()
    {
        var cursor = _helper.Target;

        if (cursor == null)
        {
            return;
        }

        if (Ghost is SosariaCharacter character)
        {
            ResurrectWatch.OnTargeted(character, _helper, cursor);
        }

        cursor.Invoke(_helper, Ghost);
    }

    private SkillStatus GiveUp(string why) => Finish(Fail(ResurrectAid.FailLine(why, Ghost?.Name)));

    /// <summary>Lets go of the ghost, drops an unanswered resurrect cursor and takes the weapon back up.</summary>
    private SkillStatus Finish(SkillStatus status)
    {
        ResurrectOffer.Release(Ghost, _helper, status == SkillStatus.Failed);

        if (_helper is not { Deleted: false, Alive: true })
        {
            return status;
        }

        if (_helper.Target is SpellTarget<Mobile> { Spell: ResurrectionSpell } cursor)
        {
            cursor.Cancel(_helper, TargetCancelType.Canceled);
        }

        GearEquip.EquipReadyWeapon(_helper);
        return status;
    }
}
