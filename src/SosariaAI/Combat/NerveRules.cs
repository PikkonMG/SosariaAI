using System;
using SosariaAI.Configuration;

namespace SosariaAI.Combat;

public enum EngageChoice
{
    Engage,
    PullStraggler,
    Decline
}

/// <summary>Everything the engage call reads, as plain numbers.</summary>
public readonly record struct EngageFacts(
    int Power,
    double Nerve,
    int AlliesOnFoe,
    int AlliesPower,
    double HitsFraction,
    bool HasHealing,
    double ThreatMultiple,
    double StartLine,
    int RoomThreat,
    int PullThreat,
    int ExtrasNearFoe,
    bool AlreadyAttacked
);

/// <summary>
/// How much trouble one character takes on. A shard where everyone reads the odds the same
/// way is wrong: the bold wade in and now and then die for it, the careful leave while they
/// still can. Nerve comes from the persona's valor and caution, the build and the power,
/// plus a small fixed offset from the serial so no two draw the line in the same place.
/// The same character always has the same nerve.
/// </summary>
public static class NerveRules
{
    public const double BaseNerve = 1.0;
    public const double ValorSwing = 0.8;
    public const double CautionSwing = 0.5;
    public const double WorkerShift = -0.2;
    public const double VeteranShift = 0.1;
    public const int ReferencePower = 120;
    public const double PowerSpan = 200;
    public const double MaxPowerShift = 0.15;
    public const int JitterSteps = 21;
    public const double JitterSpan = 0.2;
    public const double JitterCentre = 0.5;
    public const double MinNerve = 0.6;
    public const double MaxNerve = 1.8;

    /// <summary>
    /// Each friend already fighting the same foe adds this share of the character's own dare
    /// A crowd swarms what no one of them would touch alone.
    /// </summary>
    public const double AllyOnFoeShare = 0.6;
    public const int MaxCountedAlliesOnFoe = 4;

    public static double Of(double valor, double caution, CharacterRole role, bool veteran, int power, long seed)
    {
        var nerve = BaseNerve +
                    (valor - PersonaDrives.NeutralValue) * ValorSwing -
                    (caution - PersonaDrives.NeutralValue) * CautionSwing +
                    PowerShift(power) +
                    Jitter(seed);

        if (role == CharacterRole.Worker)
        {
            nerve += WorkerShift;
        }

        if (veteran)
        {
            nerve += VeteranShift;
        }

        return Math.Clamp(nerve, MinNerve, MaxNerve);
    }

    public static double PowerShift(int power) =>
        Math.Clamp((power - ReferencePower) / PowerSpan, -MaxPowerShift, MaxPowerShift);

    /// <summary>A fixed offset in [-JitterSpan/2, +JitterSpan/2] from the serial.</summary>
    public static double Jitter(long seed)
    {
        var step = (int)(Math.Abs(seed) % JitterSteps);
        return ((double)step / (JitterSteps - 1) - JitterCentre) * JitterSpan;
    }

    /// <summary>The power this character believes it has: its own scaled by nerve, raised by friends on the foe.</summary>
    public static int DarePower(int power, double nerve, int alliesOnFoe)
    {
        var allies = Math.Clamp(alliesOnFoe, 0, MaxCountedAlliesOnFoe);
        return (int)(Math.Max(0, power) * nerve * (1 + AllyOnFoeShare * allies));
    }

    /// <summary>
    /// Engage the room, pull one foe out of it, or leave it. Whoever is already being hit
    /// fights. The pull uses the most isolated foe's own threat.
    /// </summary>
    public static EngageChoice Decide(EngageFacts facts)
    {
        if (facts.AlreadyAttacked)
        {
            return EngageChoice.Engage;
        }

        var dare = DarePower(facts.Power, facts.Nerve, facts.AlliesOnFoe);

        if (ThreatRating.ShouldEngage(
                dare,
                facts.RoomThreat,
                facts.HitsFraction,
                facts.HasHealing,
                facts.AlliesPower,
                facts.ThreatMultiple,
                facts.StartLine,
                alreadyAttacked: false))
        {
            return EngageChoice.Engage;
        }

        if (FightPullRules.NeedsIsolate(facts.ExtrasNearFoe) &&
            FightPullRules.CanPullOne(
                dare,
                facts.PullThreat,
                facts.RoomThreat,
                facts.HitsFraction,
                facts.HasHealing,
                facts.AlliesPower,
                facts.ThreatMultiple,
                facts.StartLine,
                alreadyAttacked: false))
        {
            return EngageChoice.PullStraggler;
        }

        return EngageChoice.Decline;
    }
}
