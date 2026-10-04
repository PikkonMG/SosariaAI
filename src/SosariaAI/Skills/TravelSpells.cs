using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Misc;
using Server.Logging;
using Server.Spells;
using Server.Spells.Fourth;
using Server.Spells.Seventh;
using Server.Spells.Sixth;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

public enum TravelSpellKind
{
    Recall,
    Mark,
    Gate
}

/// <summary>How the last travel cast of a person ended.</summary>
public enum TravelCastOutcome
{
    None,
    Casting,
    Succeeded,
    Fizzled
}

/// <summary>
/// Recall, Mark and Gate Travel cast as a player casts them: the engine spell speaks its
/// words, takes mana and reagents (or the scroll), and when the target cursor comes up the
/// rune is picked. A runebook entry is cast from the book, as its Recall and Gate buttons
/// do, or with one of its charges, as the charge button does; no cursor comes up. A fizzle,
/// a disturbed chant or a refused place ends the cast; the caller decides whether to cast
/// again or walk. The cast is watched on a short timer, so a cast begun from any skill
/// finishes on its own.
/// </summary>
public static class TravelSpells
{
    public const int AnswerPollMilliseconds = 250;

    /// <summary>Polls before an unanswered cast is given up: the words plus a slow think.</summary>
    public const int AnswerPolls = 40;

    /// <summary>A gate stands on the caster's own tile, or beside it.</summary>
    public const int GateBesideTiles = 1;

    public const string LostRuneWhy = "lost the rune";
    public const string LostWordsWhy = "lost the words";
    public const string NoCursorWhy = "no cursor";
    public const string AwayWhy = "out of the world";
    public const string CastingWhy = "already casting";
    public const string FightingWhy = "fighting";
    public const string CriminalWhy = "a criminal";
    public const string HeatWhy = "in the heat of battle";
    public const string OverloadedWhy = "too heavy to move";
    public const string FrozenWhy = "frozen";
    public const string RecoveringWhy = "still recovering from a cast";
    public const string ManaWhy = "too little mana";
    public const string BookRestWhy = "the runebook is still resting";
    public const string NoMeansWhy = "no scroll, charge or reagents";
    public const string PlaceWhy = "the place refuses the travel";

    /// <summary>
    /// Someone stands on the landing: the engine refuses a recall or gate onto a person. A gang
    /// recalling to one camp rune met the first of them on the tile, and each one after gave up
    /// its run with "the place refuses the travel". The lander steps off in a moment.
    /// </summary>
    public const string LandingTakenWhy = "someone stands on the landing";

    /// <summary>Recall takes only bonded pets along: a tamer with an unbonded pet out gates or walks.</summary>
    public const string StrandsPetsWhy = "a recall would leave its pets behind";

    private static readonly ILogger logger = SosariaLog.For(typeof(TravelSpells));
    private static readonly Dictionary<Serial, Pending> Answering = new();
    private static readonly Dictionary<Serial, TravelCastOutcome> Outcomes = new();

    public static bool IsCasting(Mobile caster) => caster != null && Answering.ContainsKey(caster.Serial);

    /// <summary>How the person's last travel cast ended. Reading it clears a finished outcome.</summary>
    public static TravelCastOutcome TakeOutcome(Mobile caster)
    {
        if (caster == null)
        {
            return TravelCastOutcome.None;
        }

        if (IsCasting(caster))
        {
            return TravelCastOutcome.Casting;
        }

        return Outcomes.Remove(caster.Serial, out var outcome) ? outcome : TravelCastOutcome.None;
    }

    /// <summary>
    /// Stops the person's travel cast: the watch on it, the cursor answer it waits for and the
    /// words of power, and forgets how the last cast ended. A skill that stops mid-cast calls
    /// it; a watch left running answered the cursor anyway, so a red called home mid-cast
    /// still landed at the camp, and the next cast read the stale outcome.
    /// </summary>
    public static void Cancel(Mobile caster)
    {
        if (caster == null)
        {
            return;
        }

        Outcomes.Remove(caster.Serial);

        if (Answering.Remove(caster.Serial, out var pending))
        {
            pending.Stop();
        }
    }

    /// <summary>
    /// Why the person could not begin a cast now, or null when it could: out of the world or
    /// dead, already casting, fighting, a criminal, or a refusal of the engine spell itself
    /// (see <see cref="EngineRefusal"/>). The engine spell says no in silence, so a check that
    /// asked less offered recall-only camps that no recall then reached.
    /// </summary>
    private static string Unsettled(SosariaCharacter caster, TravelSpellKind kind)
    {
        if (caster is not { Deleted: false, Alive: true } || !People.InWorld(caster))
        {
            return AwayWhy;
        }

        if (caster.Spell != null || caster.Target != null || IsCasting(caster))
        {
            return CastingWhy;
        }

        if (caster.Combatant is { Deleted: false, Alive: true })
        {
            return FightingWhy;
        }

        if (caster.Criminal)
        {
            return CriminalWhy;
        }

        var spell = Create(kind, caster, null);

        return EngineRefusal(
            kind,
            TravelHeat.Hot(caster),
            StaminaSystem.IsOverloaded(caster),
            caster.Paralyzed || caster.Frozen,
            Core.TickCount - caster.NextSpellTime < 0,
            caster.Mana,
            spell.ScaleMana(spell.GetMana())
        );
    }

    /// <summary>
    /// What the engine spell refuses at its start, or null when it begins: Recall and Gate in
    /// the heat of battle (a blow on a player in the last half minute), Recall with a pack too
    /// heavy to walk, any spell while frozen, before the last cast has worn off, or short of
    /// its mana. Pure.
    /// </summary>
    public static string EngineRefusal(
        TravelSpellKind kind,
        bool combatHeat,
        bool overloaded,
        bool frozen,
        bool recovering,
        int mana,
        int manaNeeded
    )
    {
        if (combatHeat && kind != TravelSpellKind.Mark)
        {
            return HeatWhy;
        }

        if (overloaded && kind == TravelSpellKind.Recall)
        {
            return OverloadedWhy;
        }

        if (frozen)
        {
            return FrozenWhy;
        }

        if (recovering)
        {
            return RecoveringWhy;
        }

        return mana < manaNeeded ? ManaWhy : null;
    }

    /// <summary>
    /// True when a cast refused for <paramref name="why"/> can begin once a short wait is over:
    /// the heat of battle cools in half a minute, a cast wears off, mana and a runebook's rest
    /// come back, a person steps off the landing. A heavy pack, a lost rune or a place that
    /// refuses travel stays as it is.
    /// </summary>
    public static bool Passes(string why) =>
        why is HeatWhy or FightingWhy or CastingWhy or RecoveringWhy or ManaWhy or BookRestWhy or LandingTakenWhy;

    /// <summary>The skill with a scroll, or the skill, the spell and the reagents.</summary>
    private static bool HasMeans(SosariaCharacter caster, TravelSpellKind kind) =>
        FindScroll(caster, kind) != null
            ? caster.Skills.Magery.Value >= ScrollMinMagery(kind)
            : CastsFromSpellbook(caster, kind);

    /// <summary>The skill, the spell in the spellbook and the reagents: a cast that uses no scroll.</summary>
    private static bool CastsFromSpellbook(SosariaCharacter caster, TravelSpellKind kind)
    {
        var spell = Create(kind, caster, null);
        return caster.Skills.Magery.Value >= BookMinMagery(kind) &&
               SpellCasting.Knows(caster, spell) &&
               SpellCasting.HasReagents(caster.Backpack, spell.Info);
    }

    /// <summary>
    /// True when the engine lets this spell work from where the person stands toward the
    /// mark: no recall or gate out of a Felucca dungeon or the Lost Lands, and no mark in
    /// them. Casting where it cannot work only burns reagents and reads as a fizzle. A mark
    /// wants a loose rune to write on. A red recalls or gates nowhere under the guards (see
    /// <see cref="RuneShelf.Barred"/>). With <paramref name="ignoreStanders"/>, a person on the
    /// landing does not count: that asks whether only a stander is in the way.
    /// </summary>
    public static bool PlaceAllows(Mobile caster, TravelSpellKind kind, TravelMark mark, bool ignoreStanders = false)
    {
        if (caster?.Map == null || mark == null)
        {
            return false;
        }

        return kind switch
        {
            TravelSpellKind.Mark => mark.Rune != null && SpellHelper.CheckTravel(caster, TravelCheckType.Mark, out _),
            TravelSpellKind.Recall => mark.Marked && mark.TargetMap != null &&
                                      LandingFree(mark.TargetMap, mark.Target, ignoreStanders) &&
                                      !RuneShelf.BarredFor(caster, mark.Target, mark.TargetMap) &&
                                      SpellHelper.CheckTravel(caster, TravelCheckType.RecallFrom, out _) &&
                                      SpellHelper.CheckTravel(caster, mark.TargetMap, mark.Target, TravelCheckType.RecallTo, out _),
            _ => mark.Marked && mark.TargetMap == caster.Map &&
                 LandingFree(mark.TargetMap, mark.Target, ignoreStanders) &&
                 !RuneShelf.BarredFor(caster, mark.Target, mark.TargetMap) &&
                 SpellHelper.CheckTravel(caster, TravelCheckType.GateFrom, out _) &&
                 SpellHelper.CheckTravel(caster, mark.TargetMap, mark.Target, TravelCheckType.GateTo, out _)
        };
    }

    /// <summary>The engine's landing test, a spawn tile a person fits on; <paramref name="ignoreStanders"/> leaves people out of it.</summary>
    private static bool LandingFree(Map map, Point3D at, bool ignoreStanders) =>
        ignoreStanders
            ? Region.Find(at, map).AllowSpawn() && map.CanFit(at, PersonBody.Height, checkBlocksFit: false, checkMobiles: false)
            : map.CanSpawnMobile(at);

    /// <summary>True when the person could cast this spell toward the mark here and now.</summary>
    public static bool CanCastToward(SosariaCharacter caster, TravelSpellKind kind, TravelMark mark) =>
        WhyNotToward(caster, kind, mark) == null;

    /// <summary>
    /// Why the person could not cast this spell toward the mark here and now, or null when it
    /// could: no rune, a recall that would leave the tamer's pets behind, not settled for a cast (see <see cref="Unsettled"/>), a runebook still
    /// resting from its last use, no way to cast it (no scroll, charge, spell or reagents), a
    /// person on the landing, or a place that refuses the travel.
    /// </summary>
    public static string WhyNotToward(SosariaCharacter caster, TravelSpellKind kind, TravelMark mark)
    {
        if (mark is not { Deleted: false })
        {
            return LostRuneWhy;
        }

        if (kind == TravelSpellKind.Recall && PetKeeper.RecallStrandsPets(caster))
        {
            return StrandsPetsWhy;
        }

        if (Unsettled(caster, kind) is { } unsettled)
        {
            return unsettled;
        }

        if (WayToward(caster, kind, mark) == BookCast.None)
        {
            return mark.InBook && kind == TravelSpellKind.Recall && mark.Book.NextUse > Core.Now ? BookRestWhy : NoMeansWhy;
        }

        return PlaceAllows(caster, kind, mark) ? null
            : PlaceAllows(caster, kind, mark, ignoreStanders: true) ? LandingTakenWhy
            : PlaceWhy;
    }

    /// <summary>
    /// How the cast toward a mark is made: a loose rune wants the spell or a scroll; a book
    /// entry takes Gate from the spellbook or a scroll, and Recall from the spellbook or a
    /// charge, never a loose scroll: the scrolls recharge the book (see
    /// <see cref="RunebookRules.HowToRecall"/>).
    /// </summary>
    private static BookCast WayToward(SosariaCharacter caster, TravelSpellKind kind, TravelMark mark)
    {
        if (!mark.InBook || kind != TravelSpellKind.Recall)
        {
            return HasMeans(caster, kind) ? BookCast.Spell : BookCast.None;
        }

        return mark.Book.NextUse > Core.Now
            ? BookCast.None
            : RunebookRules.HowToRecall(
                CastsFromSpellbook(caster, kind),
                mark.Book.CurCharges,
                caster.Backpack?.FindItemByType<RecallScroll>() != null,
                caster.Skills.Magery.Value
            );
    }

    /// <summary>Begins the words of power toward <paramref name="mark"/>. False when the cast could not start.</summary>
    public static bool Begin(SosariaCharacter caster, TravelSpellKind kind, TravelMark mark)
    {
        if (!CanCastToward(caster, kind, mark))
        {
            return false;
        }

        var from = caster.Location;
        var spell = CreateToward(caster, kind, mark);

        if (!spell.Cast())
        {
            return false;
        }

        mark.Book?.OnTravel();
        Outcomes.Remove(caster.Serial);
        var pending = new Pending(caster, kind, spell, mark, from);
        Timer.StartTimer(
            TimeSpan.FromMilliseconds(AnswerPollMilliseconds),
            TimeSpan.FromMilliseconds(AnswerPollMilliseconds),
            AnswerPolls,
            pending.Poll,
            out pending.Token
        );
        Answering[caster.Serial] = pending;
        return true;
    }

    public static double BookMinMagery(TravelSpellKind kind) =>
        kind switch
        {
            TravelSpellKind.Recall => RecallRules.MinMagery,
            TravelSpellKind.Mark => MarkRules.MinMagery,
            _ => GateRules.MinMagery
        };

    public static double ScrollMinMagery(TravelSpellKind kind) =>
        kind switch
        {
            TravelSpellKind.Recall => RecallRules.ScrollMinMagery,
            TravelSpellKind.Mark => MarkRules.ScrollMinMagery,
            _ => GateRules.ScrollMinMagery
        };

    public static string SpellName(TravelSpellKind kind) =>
        kind switch
        {
            TravelSpellKind.Recall => RecallRules.Kind,
            TravelSpellKind.Mark => SkillKinds.Mark,
            _ => GateRules.Kind
        };

    /// <summary>A Gate Travel gate on or beside the caster's tile, or null. A public moongate is not one.</summary>
    public static Moongate GateBeside(Mobile caster)
    {
        if (!People.InWorld(caster))
        {
            return null;
        }

        foreach (var gate in caster.Map.GetItemsInRange<GateTravelMoongate>(caster.Location, GateBesideTiles))
        {
            if (gate is { Deleted: false })
            {
                return gate;
            }
        }

        return null;
    }

    private static Spell Create(TravelSpellKind kind, Mobile caster, Item scroll) =>
        kind switch
        {
            TravelSpellKind.Recall => new RecallSpell(caster, scroll),
            TravelSpellKind.Mark => new MarkSpell(caster, scroll),
            _ => new GateTravelSpell(caster, scroll)
        };

    /// <summary>
    /// The engine spell for a mark. A book entry is cast the way the runebook gump casts it:
    /// Recall or Gate on the entry, or Recall on a charge with the book standing in for the
    /// scroll, after the empty book takes the pack's scrolls.
    /// </summary>
    private static Spell CreateToward(SosariaCharacter caster, TravelSpellKind kind, TravelMark mark)
    {
        var scroll = FindScroll(caster, kind);

        if (!mark.InBook)
        {
            return Create(kind, caster, scroll);
        }

        if (kind == TravelSpellKind.Gate)
        {
            return new GateTravelSpell(caster, mark.Entry, scroll);
        }

        var way = WayToward(caster, kind, mark);

        if (way == BookCast.Spell)
        {
            return new RecallSpell(caster, mark.Entry);
        }

        if (RunebookRules.RechargesFirst(way, mark.Book.CurCharges))
        {
            RuneShelf.Recharge(caster, mark.Book);
        }

        return new RecallSpell(caster, mark.Entry, mark.Book, mark.Book);
    }

    private static Item FindScroll(Mobile caster, TravelSpellKind kind)
    {
        var pack = caster?.Backpack;

        if (pack == null)
        {
            return null;
        }

        return kind switch
        {
            TravelSpellKind.Recall => pack.FindItemByType<RecallScroll>(),
            TravelSpellKind.Mark => pack.FindItemByType<MarkScroll>(),
            _ => pack.FindItemByType<GateTravelScroll>()
        };
    }

    /// <summary>The line a recall that took writes.</summary>
    public static string RecalledLine(Point3D from, Point3D to) => $"recalled from {from} to {to}";

    /// <summary>The line a gate that opened writes.</summary>
    public static string GateOpenedLine(Point3D to) => $"opened a gate to {to}";

    /// <summary>The line a mark that took writes.</summary>
    public static string MarkedLine(Point3D at) => $"marked a rune at {at}";

    /// <summary>
    /// The line a cast that did not take writes: "fizzled recall", "fizzled gate" or
    /// "fizzled mark", then why when it was not the skill check.
    /// </summary>
    public static string FizzledLine(TravelSpellKind kind, string why) =>
        string.IsNullOrEmpty(why)
            ? $"fizzled {SpellName(kind).ToLowerInvariant()}"
            : $"fizzled {SpellName(kind).ToLowerInvariant()} ({why})";

    private static void Finish(Pending pending, TravelCastOutcome outcome, string line)
    {
        pending.Token.Cancel();
        Answering.Remove(pending.Caster.Serial);
        Outcomes[pending.Caster.Serial] = outcome;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} {Line}", pending.Caster.Name, line);
        }
    }

    private sealed class Pending
    {
        private readonly TravelSpellKind _kind;
        private readonly Spell _spell;
        private readonly TravelMark _mark;
        private readonly Point3D _castFrom;
        private int _polls;

        public Pending(SosariaCharacter caster, TravelSpellKind kind, Spell spell, TravelMark mark, Point3D castFrom)
        {
            Caster = caster;
            _kind = kind;
            _spell = spell;
            _mark = mark;
            _castFrom = castFrom;
        }

        public SosariaCharacter Caster { get; }

        public TimerExecutionToken Token;

        /// <summary>Ends the watch and breaks off the words, or takes down the cursor they raised.</summary>
        public void Stop()
        {
            Token.Cancel();

            if (Caster.Spell == _spell)
            {
                _spell.Disturb(DisturbType.Unspecified);
            }
        }

        public void Poll()
        {
            _polls++;

            if (Caster.Deleted || _mark.Deleted)
            {
                Finish(this, TravelCastOutcome.Fizzled, FizzledLine(_kind, LostRuneWhy));
                return;
            }

            if (_mark.InBook)
            {
                PollBookCast();
                return;
            }

            if (Caster.Spell == _spell && _spell.State == SpellState.Sequencing && Caster.Target is { } cursor)
            {
                var from = Caster.Location;
                var fromMap = Caster.Map;
                cursor.Invoke(Caster, _mark.Rune);

                if (Worked(from, fromMap))
                {
                    ShelveMarked();
                    Finish(this, TravelCastOutcome.Succeeded, SuccessLine(from));
                }
                else
                {
                    Finish(this, TravelCastOutcome.Fizzled, FizzledLine(_kind, null));
                }

                return;
            }

            if (Caster.Spell != _spell)
            {
                Finish(this, TravelCastOutcome.Fizzled, FizzledLine(_kind, LostWordsWhy));
                return;
            }

            if (_polls >= AnswerPolls)
            {
                Finish(this, TravelCastOutcome.Fizzled, FizzledLine(_kind, NoCursorWhy));
            }
        }

        /// <summary>A book cast needs no cursor: once the spell is over, the person stands where it took it.</summary>
        private void PollBookCast()
        {
            if (Caster.Spell != _spell)
            {
                if (Worked(_castFrom, Caster.Map))
                {
                    Finish(this, TravelCastOutcome.Succeeded, SuccessLine(_castFrom));
                }
                else
                {
                    Finish(this, TravelCastOutcome.Fizzled, FizzledLine(_kind, null));
                }

                return;
            }

            if (_polls >= AnswerPolls)
            {
                Finish(this, TravelCastOutcome.Fizzled, FizzledLine(_kind, LostWordsWhy));
            }
        }

        /// <summary>A rune just marked goes into the book, when the person carries one with room.</summary>
        private void ShelveMarked()
        {
            if (_kind == TravelSpellKind.Mark)
            {
                RuneShelf.Shelve(Caster, _mark.Rune);
            }
        }

        private bool Worked(Point3D from, Map fromMap) =>
            _kind switch
            {
                TravelSpellKind.Recall => GateHopRules.Carried(from, Caster.Location, fromMap == Caster.Map),
                TravelSpellKind.Mark => _mark.Rune.Marked,
                _ => GateBeside(Caster) != null
            };

        private string SuccessLine(Point3D from) =>
            _kind switch
            {
                TravelSpellKind.Recall when _mark.InBook => RunebookRules.RecalledFromBookLine(from, Caster.Location),
                TravelSpellKind.Recall => RecalledLine(from, Caster.Location),
                TravelSpellKind.Mark => MarkedLine(Caster.Location),
                _ => GateOpenedLine(_mark.Target)
            };
    }
}
