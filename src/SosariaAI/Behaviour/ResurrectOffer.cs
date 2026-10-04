using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells.Eighth;
using SosariaAI.Combat;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// A living character who sees a ghost it may help walks over and raises it with a real
/// Resurrection cast or a real bandage. A ghost that walks up and asks gets the same
/// answer, and a red ghost calls a gang mate or a red near it over (<see cref="CallRedHelper"/>).
/// Only one helper works on one ghost at a time.
/// </summary>
public static class ResurrectOffer
{
    /// <summary>A helper that failed a ghost leaves that ghost alone for this long.</summary>
    public static readonly TimeSpan GiveUpFor = TimeSpan.FromMinutes(3);

    private static readonly Dictionary<Serial, SosariaCharacter> HelperOf = new();
    private static readonly Dictionary<(Serial Helper, Serial Ghost), DateTime> GaveUpUntil = new();

    /// <summary>The spell, else the bandage, else nothing, from the helper's skills, book and pack.</summary>
    public static AidMethod MethodFor(Mobile helper)
    {
        if (helper is not { Deleted: false, Alive: true } || helper.Backpack == null)
        {
            return AidMethod.None;
        }

        var magery = helper.Skills.Magery.Value;
        var healing = helper.Skills.Healing.Value;
        var anatomy = helper.Skills.Anatomy.Value;

        if (!ResurrectAid.SkilledToRaise(magery, healing, anatomy))
        {
            return AidMethod.None;
        }

        var spell = new ResurrectionSpell(helper);
        return ResurrectAid.MethodFor(
            magery,
            helper.Mana,
            SpellCasting.Knows(helper, spell),
            SpellCasting.HasReagents(helper.Backpack, spell.Info),
            healing,
            anatomy,
            helper.Backpack.FindItemByType<Bandage>() != null
        );
    }

    /// <summary>Party, guild or a warm opinion of the fallen by name.</summary>
    public static bool IsBonded(SosariaCharacter helper, Mobile ghost)
    {
        var party = GameParty.Of(helper);

        if (party != null && party == GameParty.Of(ghost))
        {
            return true;
        }

        if (helper.Guild != null && helper.Guild == ghost.Guild)
        {
            return true;
        }

        if (ghost is SosariaCharacter fallen)
        {
            var planned = Party.FindByMember(helper.CharacterId);

            if (planned != null && planned == Party.FindByMember(fallen.CharacterId))
            {
                return true;
            }
        }

        return BondRules.IsWarm(Recall.ScoreOf(MemoryStore.Shared, helper, ghost));
    }

    public static bool WillAid(SosariaCharacter helper, Mobile ghost, bool asked) =>
        ResurrectAid.Willing(
            PkRules.IsRed(helper.Kills),
            PkRules.IsRed(ghost.Kills),
            TookPartInKill(helper, ghost) || FactionWar.AreFoes(helper, ghost),
            WantedUnderGuards(ghost),
            IsBonded(helper, ghost),
            helper.Disposition,
            asked
        );

    /// <summary>Who answers for the death: the owner of the pet that landed the last blow, else the one that landed it.</summary>
    public static Mobile KillerOf(Mobile ghost) =>
        ghost?.LastKiller is BaseCreature { ControlMaster: { } master } ? master : ghost?.LastKiller;

    /// <summary>
    /// The helper killed the ghost, set its pet on it, or hurt it in the fight that killed it
    /// (the engine keeps each damager for a while). None of them raises the body it made.
    /// </summary>
    public static bool TookPartInKill(Mobile helper, Mobile ghost) =>
        helper != null && ghost != null &&
        (ghost.LastKiller == helper || KillerOf(ghost) == helper || ghost.FindDamageEntryFor(helper) != null);

    /// <summary>The one who killed the ghost, alive and still within a plea's reach of it.</summary>
    public static bool KillerNear(Mobile ghost) =>
        KillerOf(ghost) is { Deleted: false, Alive: true } killer &&
        killer.Map == ghost.Map &&
        ghost.InRange(killer, GhostRules.AidSearchRange);

    /// <summary>A living foe near the helper whose fight is on the helper: its own target, or anyone who hit it and still aims at it.</summary>
    public static Mobile FoeOn(Mobile helper)
    {
        if (helper == null)
        {
            return null;
        }

        if (FightsHelper(helper, helper.Combatant))
        {
            return helper.Combatant;
        }

        foreach (var info in helper.Aggressors)
        {
            if (FightsHelper(helper, info.Attacker))
            {
                return info.Attacker;
            }
        }

        return null;
    }

    /// <summary>This fallen person is a criminal or a red under the guards now (<see cref="ResurrectAid.WantedUnderGuards"/>).</summary>
    public static bool WantedUnderGuards(Mobile fallen) =>
        ResurrectAid.WantedUnderGuards(fallen.Criminal, PkRules.IsRed(fallen.Kills), SosariaCharacter.UnderGuards(fallen));

    /// <summary>Why this raise must stop now (<see cref="ResurrectAid.Hindrance"/>), or null.</summary>
    public static string Hindrance(Mobile helper, Mobile ghost) =>
        ResurrectAid.Hindrance(FoeOn(helper)?.Name, KillerNear(ghost), WantedUnderGuards(ghost));

    private static bool FightsHelper(Mobile helper, Mobile foe) =>
        foe is { Deleted: false, Alive: true } && foe.Combatant == helper && foe.Map == helper.Map &&
        helper.InRange(foe, ResurrectAid.FoeRange);

    /// <summary>The character working on this ghost now, or null.</summary>
    public static SosariaCharacter HelperFor(Mobile ghost)
    {
        if (ghost == null || !HelperOf.TryGetValue(ghost.Serial, out var helper))
        {
            return null;
        }

        if (helper is { Deleted: false, Alive: true } && helper.Routine?.CurrentSkill is ResurrectAidSkill aid &&
            aid.Ghost == ghost)
        {
            return helper;
        }

        HelperOf.Remove(ghost.Serial);
        return null;
    }

    /// <summary>
    /// Starts a raise when the helper is free and willing. A stranger's plea is answered
    /// from any errand but a hunt; unasked, only an idle person walks over.
    /// </summary>
    public static bool TryStartAid(SosariaCharacter helper, Mobile ghost, bool asked)
    {
        if (!MayStart(helper, ghost) || !asked && !WorldPlay.IsIdle(helper) || !WillAid(helper, ghost, asked))
        {
            return false;
        }

        var skill = new ResurrectAidSkill(ghost);
        HelperOf[ghost.Serial] = helper;
        WorldPlay.StartWork(helper, skill);
        return true;
    }

    /// <summary>Ends a helper's claim on a ghost. A failed helper does not try that ghost again for a while.</summary>
    public static void Release(Mobile ghost, SosariaCharacter helper, bool failed)
    {
        if (ghost == null || helper == null)
        {
            return;
        }

        if (HelperOf.TryGetValue(ghost.Serial, out var current) && current == helper)
        {
            HelperOf.Remove(ghost.Serial);
        }

        if (failed)
        {
            GaveUpUntil[(helper.Serial, ghost.Serial)] = Core.Now + GiveUpFor;
        }
        else
        {
            GaveUpUntil.Remove((helper.Serial, ghost.Serial));
        }
    }

    /// <summary>On the world scan: the nearest manifest ghost in sight that this character will raise.</summary>
    public static void Consider(SosariaCharacter helper)
    {
        if (helper?.Map == null || helper.Map == Map.Internal || !helper.Alive ||
            helper.Routine?.CurrentSkill is ResurrectAidSkill || MethodFor(helper) == AidMethod.None)
        {
            return;
        }

        Mobile best = null;
        var bestDistance = int.MaxValue;

        foreach (var mobile in helper.Map.GetMobilesInRange(helper.Location, ResurrectAid.GhostSeekRange))
        {
            if (mobile is not PlayerMobile { Deleted: false, Alive: false } ghost || !helper.CanSee(ghost) ||
                !helper.InLOS(ghost) || HelperFor(ghost) != null)
            {
                continue;
            }

            var distance = (int)helper.GetDistanceToSqrt(ghost);

            if (distance < bestDistance && WillAid(helper, ghost, asked: false))
            {
                best = ghost;
                bestDistance = distance;
            }
        }

        if (best != null)
        {
            TryStartAid(helper, best, asked: false);
        }
    }

    /// <summary>
    /// A red ghost calls a living red over to raise it: a gang mate within
    /// <see cref="ResurrectAid.GangAidRange"/>, in sight or not, before any other red, which must
    /// stand in sight within the sight scan; of two alike the nearer (<see cref="ResurrectAid.CallsBefore"/>).
    /// A red that fights stays in its fight; the killer, a sworn foe, a red without the means and
    /// a red busy with a hunt or another ghost are not called, and no red is called while the
    /// ghost is wanted under the guards (<see cref="WillAid"/>). The owner: reds help reds up.
    /// Returns the red that set out, or null.
    /// </summary>
    public static SosariaCharacter CallRedHelper(SosariaCharacter ghost)
    {
        if (FindRedHelper(ghost, out var mate) is not { } best || !TryStartAid(best, ghost, asked: true))
        {
            return null;
        }

        WorldPlay.Log($"{ghost.Name} calls {(mate ? "gang mate " : "fellow red ")}{best.Name} over to raise it");
        return best;
    }

    /// <summary>
    /// The red a red ghost would call over now (<see cref="CallRedHelper"/>), without calling it,
    /// and whether it is a gang mate; null when none would come.
    /// </summary>
    public static SosariaCharacter FindRedHelper(SosariaCharacter ghost, out bool gangMate)
    {
        gangMate = false;

        if (ghost is not { Deleted: false, IsGhost: true } || ghost.Map == null || ghost.Map == Map.Internal ||
            !PkRules.IsRed(ghost.Kills) || HelperFor(ghost) != null)
        {
            return null;
        }

        SosariaCharacter best = null;
        var bestMate = false;
        var bestDistance = int.MaxValue;

        foreach (var mobile in ghost.Map.GetMobilesInRange(ghost.Location, ResurrectAid.GangAidRange))
        {
            if (mobile is not SosariaCharacter { Deleted: false, Alive: true, Combatant: null } red || !PkRules.IsRed(red.Kills))
            {
                continue;
            }

            var mate = PkGangRules.SameGang(ghost.OutlawGang, red.OutlawGang);
            var distance = NavMetric.Chebyshev(ghost.Location, red.Location);

            if (distance > ResurrectAid.RedCallRange(mate) || !ResurrectAid.CallsBefore(mate, distance, bestMate, bestDistance) ||
                !mate && !(ghost.CanSee(red) && ghost.InLOS(red)) || !MayStart(red, ghost) || !WillAid(red, ghost, asked: true))
            {
                continue;
            }

            best = red;
            bestMate = mate;
            bestDistance = distance;
        }

        gangMate = bestMate;
        return best;
    }

    private static bool MayStart(SosariaCharacter helper, Mobile ghost) =>
        helper is { Deleted: false, Alive: true } &&
        ghost is PlayerMobile { Deleted: false, Alive: false } &&
        ghost != helper &&
        ghost.Map == helper.Map &&
        helper.Motor.Action == CharacterAction.Wander &&
        !RoutineDriver.IsHunting(helper) &&
        helper.Routine?.CurrentSkill is not (ResurrectAidSkill or GhostSkill) &&
        HelperFor(ghost) == null &&
        !(GaveUpUntil.TryGetValue((helper.Serial, ghost.Serial), out var until) && Core.Now < until) &&
        !JobTargetRest.Rests(helper, ResurrectAidSkill.SkillName, JobTargetRest.KeyOf(ghost), Core.Now) &&
        Hindrance(helper, ghost) == null &&
        MethodFor(helper) != AidMethod.None;
}
