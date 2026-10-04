using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SosariaAI.Behaviour;

/// <summary>What one crew member does about the group's gate this think.</summary>
public enum GateTurn
{
    /// <summary>No group gate concerns this person.</summary>
    None,

    /// <summary>Stand by: the words are spoken, or others go first, or the rest are still to come.</summary>
    Hold,

    /// <summary>The person just stepped through.</summary>
    Through
}

/// <summary>
/// One gate for the whole group, the way a 1999 party travelled: whoever of the group can cast
/// Gate Travel opens it at the muster, the leader walks through first, the rest after it, and
/// the caster holds the gate until every member stepped through, then follows. A fizzle is
/// cast again a few times; after that the group recalls when everyone can, else walks. Pure.
/// </summary>
public static class PartyGateRules
{
    public const int NoOne = -1;

    private static readonly Regex GateWord = new(
        @"\b(gate|gates|gated|gating)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    /// <summary>Casts before the group gives the gate up. A caster at the era's seventh-circle floor fizzles more than it opens.</summary>
    public const int MaxCasts = 5;

    /// <summary>A member this close to the leader at the cast is waited for at the gate.</summary>
    public const int CrewTiles = LfgRules.MusterRange * 2;

    /// <summary>The engine keeps a Gate Travel gate open this long in the era.</summary>
    public static readonly TimeSpan GateLife = TimeSpan.FromSeconds(30);

    /// <summary>The caster steps in by this time, whoever is left, before the gate closes behind it.</summary>
    public static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(24);

    /// <summary>Words of power that bring no answer in this time are given up.</summary>
    public static readonly TimeSpan CastLimit = TimeSpan.FromSeconds(20);

    /// <summary>The leader opens the gate when it can; otherwise the first member in party order who can.</summary>
    public static int PickGater(IReadOnlyList<bool> canGate, int leaderIndex)
    {
        if (canGate == null)
        {
            return NoOne;
        }

        if (leaderIndex >= 0 && leaderIndex < canGate.Count && canGate[leaderIndex])
        {
            return leaderIndex;
        }

        for (var i = 0; i < canGate.Count; i++)
        {
            if (canGate[i])
            {
                return i;
            }
        }

        return NoOne;
    }

    /// <summary>
    /// True for a line that promises the group a gate ("on me, gating to shame soon"): said
    /// only when someone of the group can open one, as a group nobody can gate for walks.
    /// </summary>
    public static bool SpeaksOfGate(string line) => !string.IsNullOrWhiteSpace(line) && GateWord.IsMatch(line);

    /// <summary>A fizzled gate is cast again while casts are left.</summary>
    public static bool CastAgain(int castsSoFar) => castsSoFar < MaxCasts;

    /// <summary>
    /// True while a caster whose next cast was refused still has the engine's pause after a
    /// spell to sit out: it waits and casts again, within <see cref="CastLimit"/>.
    /// </summary>
    public static bool WaitsToRecast(long nowTicks, long nextSpellTicks) => nowTicks < nextSpellTicks;

    /// <summary>
    /// Who of those the caster waits for may step in now: the leader first, so nobody lands
    /// at the door ahead of it; the rest once the leader is through. The caster itself goes
    /// last (see <see cref="CasterGoes"/>).
    /// </summary>
    public static bool MayStep(bool isLeader, bool leaderThrough) => isLeader || leaderThrough;

    /// <summary>The caster steps in once every member it waits for is through, or its hold is up.</summary>
    public static bool CasterGoes(int waitingFor, int through, TimeSpan held) =>
        through >= waitingFor || held >= HoldLimit;

    /// <summary>A gate not open within the cast limit, or open past its life, is over.</summary>
    public static bool Expired(bool open, TimeSpan sinceProgress) =>
        sinceProgress >= (open ? GateLife : CastLimit);

    /// <summary>The line a group crossing writes, easy to count.</summary>
    public static string GatedLine(string leader, int count, string place) =>
        $"{leader}'s party of {count} gated to {place}";

    /// <summary>The line a group gate that never carried the group writes.</summary>
    public static string FellThroughLine(string leader, string place, string why) =>
        $"{leader}'s party gate to {place} fell through: {why}";
}
