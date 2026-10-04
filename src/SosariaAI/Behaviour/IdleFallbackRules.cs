namespace SosariaAI.Behaviour;

/// <summary>
/// What a character does when the scorer offers nothing. It wanders, unless a threat in sight
/// forces a run it may still take: then it runs. Wander refuses to start with that threat in
/// sight, so a wander fallback there failed every four seconds, 181 times for one character
/// in a dungeon, while nothing ever started the run. Pure.
/// </summary>
public static class IdleFallbackRules
{
    public static bool FleesInstead(bool threatForcesRun, bool mayStillFlee) => threatForcesRun && mayStillFlee;
}
