using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Spells;
using Server.Spells.Eighth;
using Server.Targeting;
using SosariaAI.Mobiles;

namespace SosariaAI.Behaviour;

/// <summary>
/// A character ghost has no client, so the resurrect gump the engine sends on a good
/// Resurrection cast or bandage goes nowhere. This watch sees the outcome instead and
/// answers the gump in code. A cast is good when it took the caster's mana: the engine
/// takes mana only after the fizzle check passed. A bandage raise is rolled with the
/// engine's own chance once its timer ran out with the healer still beside the body. A
/// ghost the guards want where it stands (<see cref="ResurrectOffer.WantedUnderGuards"/>)
/// says no to the gump, as a player would: the raise is asked again at that moment, since
/// a bandage runs for seconds after the helper last looked.
/// </summary>
public static class ResurrectWatch
{
    /// <summary>Time past the bandage delay before its outcome is read.</summary>
    public static readonly TimeSpan BandageSlack = TimeSpan.FromMilliseconds(250);

    public const string SpellReason = "raised by a spell";
    public const string BandageReason = "raised with bandages";

    private static readonly Dictionary<Serial, Attempt> Attempts = new();

    private sealed class Attempt(Mobile healer, int manaBefore, bool spell)
    {
        public Mobile Healer { get; } = healer;

        public int ManaBefore { get; } = manaBefore;

        public bool Spell { get; } = spell;

        public BandageContext Bandage { get; set; }
    }

    /// <summary>
    /// Call just before a target cursor lands on a ghost: from a helper about to answer its
    /// own cursor, and from the ghost's CheckTarget when anyone else does.
    /// </summary>
    public static void OnTargeted(SosariaCharacter ghost, Mobile healer, Target target)
    {
        if (ghost is not { Deleted: false, IsGhost: true } || healer == null || healer == ghost || target == null)
        {
            return;
        }

        // The helper reports its own cursor, then the engine reports the same one again.
        if (Attempts.TryGetValue(ghost.Serial, out var pending) && pending.Healer == healer && pending.Bandage == null)
        {
            return;
        }

        var attempt = new Attempt(healer, healer.Mana, target is SpellTarget<Mobile> { Spell: ResurrectionSpell });
        Attempts[ghost.Serial] = attempt;
        Timer.StartTimer(() => AfterTarget(ghost, attempt));
    }

    private static void AfterTarget(SosariaCharacter ghost, Attempt attempt)
    {
        if (!IsCurrent(ghost, attempt))
        {
            return;
        }

        if (attempt.Spell)
        {
            Finish(ghost, attempt, attempt.Healer.Mana < attempt.ManaBefore, SpellReason);
            return;
        }

        var context = BandageContext.GetContext(attempt.Healer);

        if (context?.Patient != ghost)
        {
            Attempts.Remove(ghost.Serial);
            return;
        }

        attempt.Bandage = context;
        Timer.StartTimer(context.Delay + BandageSlack, () => AfterBandage(ghost, attempt));
    }

    private static void AfterBandage(SosariaCharacter ghost, Attempt attempt)
    {
        if (!IsCurrent(ghost, attempt))
        {
            return;
        }

        var healer = attempt.Healer;
        var now = BandageContext.GetContext(healer);

        if (now == attempt.Bandage)
        {
            Timer.StartTimer(BandageSlack, () => AfterBandage(ghost, attempt));
            return;
        }

        // A newer bandage on someone else stopped this one before it finished.
        if (now != null)
        {
            Attempts.Remove(ghost.Serial);
            return;
        }

        var raised = healer.Alive &&
                     healer.Map == ghost.Map &&
                     healer.InRange(ghost, Bandage.Range) &&
                     ResurrectAid.BandageRaises(
                         healer.Skills.Healing.Value,
                         healer.Skills.Anatomy.Value,
                         attempt.Bandage.Slips,
                         Utility.RandomDouble()
                     );
        Finish(ghost, attempt, raised, BandageReason);
    }

    private static void Finish(SosariaCharacter ghost, Attempt attempt, bool raised, string reason)
    {
        Attempts.Remove(ghost.Serial);

        if (raised && !ResurrectOffer.WantedUnderGuards(ghost) &&
            ghost.Map?.CanFit(ghost.Location, PersonBody.Height, false, false) == true)
        {
            ghost.RestoreLife(reason, attempt.Healer);
        }
    }

    private static bool IsCurrent(SosariaCharacter ghost, Attempt attempt) =>
        Attempts.TryGetValue(ghost.Serial, out var current) && current == attempt && !ghost.Deleted && ghost.IsGhost;
}
