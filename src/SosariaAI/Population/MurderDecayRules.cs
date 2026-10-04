using System;
using SosariaAI.Combat;

namespace SosariaAI.Population;

/// <summary>The engine's two murder decay marks, as offsets on the owner's game time.</summary>
public readonly record struct MurderMarks(TimeSpan ShortTermElapse, TimeSpan LongTermElapse);

/// <summary>
/// Murder counts decay with time played, as the engine's own clock does for a player: one
/// short-term murder per short-term period and one long-term murder per long-term period of
/// logged-in time (the engine's <c>murderSystem</c> settings). The engine counts that time
/// only while a client is attached, so a character's marks are moved closer by the time it
/// was logged in instead; the engine's own decay step then reads them. A murderer the plan
/// made red keeps killing on a real shard, so its long-term clock stops at the red line; its
/// short-term murders still decay.
/// </summary>
public static class MurderDecayRules
{
    /// <summary>Logged-in time since the last count: none on the first count, or when the clock ran back.</summary>
    public static TimeSpan LoggedInSince(DateTime lastCounted, DateTime now) =>
        lastCounted == default || now <= lastCounted ? TimeSpan.Zero : now - lastCounted;

    public static bool ShortTermRuns(int shortTermMurders) => shortTermMurders > 0;

    public static bool LongTermRuns(int kills, bool planRed) =>
        kills > 0 && (!planRed || kills > PkRules.MurdersToRed);

    /// <summary>Each running mark moves closer by the logged-in time, the same as the engine's game time moving on.</summary>
    public static MurderMarks Advance(MurderMarks marks, TimeSpan loggedIn, int shortTermMurders, int kills, bool planRed) =>
        new(
            ShortTermRuns(shortTermMurders) ? marks.ShortTermElapse - loggedIn : marks.ShortTermElapse,
            LongTermRuns(kills, planRed) ? marks.LongTermElapse - loggedIn : marks.LongTermElapse
        );

    public static bool WentBlue(int killsBefore, int killsAfter) =>
        PkRules.IsRed(killsBefore) && !PkRules.IsRed(killsAfter);
}
