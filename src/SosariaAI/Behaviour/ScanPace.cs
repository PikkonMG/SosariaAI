namespace SosariaAI.Behaviour;

/// <summary>
/// Sector scans do not need think rate. A wander think fires four times a second, and a
/// map scan for greets, musings, threats and rivals on each one would take the loop's
/// whole budget at a thousand walkers. Danger stays near-instant; the rest run on a
/// slower per-person cadence whose phase comes from the serial.
/// </summary>
public static class ScanPace
{
    /// <summary>Flee and threat checks — slow enough to halve cost, fast enough to answer a PK.</summary>
    public const int DangerMs = 1000;

    /// <summary>Outlaw, guild-war, lawful and party scans.</summary>
    public const int WorldMs = 1000;

    /// <summary>Greetings, musings and nearby-player notice.</summary>
    public const int AmbientMs = 2500;

    /// <summary>
    /// The first call after spawn or load fires at once so a character wakes aware,
    /// then the serial offset spreads the cohort's phases so a town does not scan on
    /// one tick.
    /// </summary>
    public static bool Due(long now, ref long nextAt, int intervalMs, long phase)
    {
        if (nextAt == 0)
        {
            nextAt = now + intervalMs + phase % intervalMs;
            return true;
        }

        if (now - nextAt < 0)
        {
            return false;
        }

        nextAt = now + intervalMs;
        return true;
    }
}
