using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Regions;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Combat;

/// <summary>
/// Shouts for the town watch and spawns a guard on the foe. Sosaria people are
/// Player=true, so the region will not pick them as the nearby crier. A red or a gray
/// person, human or character, is a lawful call; any other person is not.
/// </summary>
public static class GuardCall
{
    /// <summary>The foe rests are short; once this many are held they are all let go.</summary>
    public const int SweepAt = 256;

    private static readonly Dictionary<Serial, DateTime> LastCall = new();
    private static readonly Dictionary<Serial, DateTime> LastCallOn = new();

    /// <summary>A spot the town watch covers: a guarded town or a public moongate.</summary>
    public static bool IsGuardedPlace(Point3D at, Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        var region = Region.Find(at, map)?.GetRegion<GuardedRegion>();
        return EnemyRules.IsGuardedFlag(region != null, region?.IsDisabled() == true);
    }

    /// <summary>
    /// True under the guards or at their line: guarded ground lies within
    /// <see cref="GuardLineRules.LineMarginTiles"/> (<see cref="GuardLineRules.LineRing"/>). A fight
    /// started there is stood down at the line at once.
    /// </summary>
    public static bool AtGuardLine(Point3D at, Map map)
    {
        if (IsGuardedPlace(at, map))
        {
            return true;
        }

        var ring = GuardLineRules.LineRing;

        for (var i = 0; i < ring.Length; i++)
        {
            if (IsGuardedPlace(new Point3D(at.X + ring[i].X, at.Y + ring[i].Y, at.Z), map))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryCall(SosariaCharacter self, Mobile foe)
    {
        if (self == null || foe == null)
        {
            return false;
        }

        var guarded = SosariaCharacter.UnderGuards(self);
        var sameMap = foe.Map == self.Map;
        var foeIsPerson = foe is PlayerMobile;

        if (!GuardCallRules.ShouldCall(
                self.Alive,
                self.IsGhost,
                guarded,
                foe.Alive && !foe.Deleted,
                sameMap,
                foe is BaseGuard,
                foeIsPerson,
                GuardCallRules.IsOutlaw(
                    foe.Murderer,
                    foe.Criminal,
                    FactionWar.LawfulFight(self, foe) || FactionWar.LawfulFight(foe, foe.Combatant)
                ),
                SosariaCharacter.UnderGuards(foe)
            ))
        {
            return false;
        }

        var now = Core.Now;

        if (!GuardCallRules.CallDue(LastCall.GetValueOrDefault(self.Serial), LastCallOn.GetValueOrDefault(foe.Serial), now))
        {
            return false;
        }

        if (LastCallOn.Count >= SweepAt)
        {
            LastCallOn.Clear();
        }

        LastCall[self.Serial] = now;
        LastCallOn[foe.Serial] = now;
        self.SpeakScripted(GuardCallRules.Shout);

        if (GuardCallRules.MarksFoeCriminal(foeIsPerson))
        {
            foe.Criminal = true;
        }

        var region = Region.Find(self.Location, self.Map)?.GetRegion<GuardedRegion>();

        if (region != null && !region.IsDisabled())
        {
            region.CheckGuardCandidate(foe);
            region.CallGuards(self.Location);
        }

        BaseGuard.Spawn(self, foe);
        return true;
    }
}
