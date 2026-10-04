using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// When each tamer practises: a session of a few let-go beasts on one taming trip, then its
/// taming rests (<see cref="TameRules.PracticeRest"/>) while it hunts, banks and trades, as a
/// player broke off a taming session. The close of the last session is the tamer's own
/// <see cref="RuleClock.TamingSession"/> clock, which the save keeps. World thread only.
/// </summary>
public static class TamePractice
{
    /// <summary>True while the tamer may practise: no session rest runs. Only a person has sessions.</summary>
    public static bool IsOpen(Mobile tamer, DateTime now) =>
        tamer is not SosariaCharacter character ||
        TimeRules.Rested(character.ClockAt(RuleClock.TamingSession), now, TameRules.PracticeRest);

    /// <summary>The tamer's practice session is over: its taming rests from now.</summary>
    public static void Close(SosariaCharacter tamer, DateTime now) => tamer.StartClock(RuleClock.TamingSession, now);
}
