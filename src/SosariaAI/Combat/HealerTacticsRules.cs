using System.Collections.Generic;

namespace SosariaAI.Combat;

/// <summary>A friend in the fight as the healer sees it: its share of hits and whether it is poisoned.</summary>
public readonly record struct MateCare(double HitsFraction, bool Poisoned);

/// <summary>
/// A mage-healer in a fight keeps its party and the friends fighting beside it standing before
/// it casts at the foe: Cure on the poisoned, Greater Heal or Heal on the wounded (the spell
/// <see cref="Behaviour.PetRules.PetSpell"/> picks), the worst off first. Any other caster heals
/// a party or gang mate in deep trouble too, when its own hits are safe: allies of the period
/// Greater Healed each other in a fight. A healer raises a fallen
/// party mate with Resurrection when nothing fights it and the killer no longer stands over the
/// ghost (<see cref="Behaviour.ResurrectAid.Hindrance"/>): the spell reaches one tile
/// (ResurrectionSpell.TargetRange), so it walks over first. A healer that only threw bolts let
/// its party fall around it. Pure.
/// </summary>
public static class HealerTacticsRules
{
    /// <summary>A friend under this share of its hits is healed: the line a fighter heals itself at.</summary>
    public const double HealMateFraction = SelfCareRules.FightHealFraction;

    /// <summary>A caster that is no healer heals a friend only under this share of its hits.</summary>
    public const double CasterHealMateFraction = 0.5;

    /// <summary>A caster that is no healer heals a friend only while its own hits stand at this share or above.</summary>
    public const double SafeOwnHitsFraction = SelfCareRules.FightHealFraction;

    /// <summary>Poison counts as this much less of the hits left: it keeps eating them.</summary>
    public const double PoisonUrgency = 0.2;

    /// <summary>A fallen party mate this close is raised in the fight; farther off it waits for the fight to end.</summary>
    public const int FightRaiseTiles = 8;

    /// <summary>The answer of <see cref="Pick"/> when nobody needs the healer.</summary>
    public const int NoPatient = -1;

    /// <summary>
    /// A healer treats a friend that is poisoned or under its heal line; any other caster only
    /// one deep in trouble (<see cref="CasterHealMateFraction"/>).
    /// </summary>
    public static bool NeedsCare(MateCare mate, bool healer) =>
        healer ? mate.Poisoned || mate.HitsFraction < HealMateFraction : mate.HitsFraction < CasterHealMateFraction;

    /// <summary>A healer always looks to its friends; any other caster only when it is clean and its own hits are safe.</summary>
    public static bool HealsMates(bool healer, double ownHitsFraction, bool ownPoisoned) =>
        healer || !ownPoisoned && ownHitsFraction >= SafeOwnHitsFraction;

    /// <summary>
    /// A heal that would make the healer a criminal is left alone, unless the healer is red
    /// already: a murderer healing its gang mate loses nothing to the flag.
    /// </summary>
    public static bool MayAid(bool crime, bool healerRed) => !crime || healerRed;

    /// <summary>How badly a friend needs the healer: lower is worse.</summary>
    public static double Urgency(MateCare mate) => mate.HitsFraction - (mate.Poisoned ? PoisonUrgency : 0);

    /// <summary>The friend to treat first: the worst off of those who need care, or <see cref="NoPatient"/>.</summary>
    public static int Pick(IReadOnlyList<MateCare> mates, bool healer)
    {
        var best = NoPatient;

        for (var i = 0; i < (mates?.Count ?? 0); i++)
        {
            if (NeedsCare(mates[i], healer) && (best == NoPatient || Urgency(mates[i]) < Urgency(mates[best])))
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// A fallen party mate is raised in the fight when the healer can cast Resurrection, nothing
    /// hinders the raise, and the ghost lies within <see cref="FightRaiseTiles"/>.
    /// </summary>
    public static bool RaisesInFight(bool canRaise, bool hindered, int ghostDistance) =>
        canRaise && !hindered && ghostDistance <= FightRaiseTiles;
}
