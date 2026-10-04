namespace SosariaAI.Spawning;

/// <summary>
/// How much a person leans toward each kind of long activity, each from 0 to 1. The
/// values are weights for a job pick, not a plan.
/// </summary>
public sealed class ActivityTendencies
{
    public const double Neutral = 0.5;

    public static ActivityTendencies Even { get; } = new(Neutral, Neutral, Neutral, Neutral, Neutral);

    public ActivityTendencies(double banking, double adventuring, double travel, double crafting, double idling)
    {
        Banking = banking;
        Adventuring = adventuring;
        Travel = travel;
        Crafting = crafting;
        Idling = idling;
    }

    /// <summary>Bank visits, bank sitting and trade at the bank.</summary>
    public double Banking { get; }

    /// <summary>Hunts, dungeons and fights.</summary>
    public double Adventuring { get; }

    /// <summary>Trips between towns, gates and sights.</summary>
    public double Travel { get; }

    /// <summary>Gathering and making things.</summary>
    public double Crafting { get; }

    /// <summary>Standing about, taverns and chat.</summary>
    public double Idling { get; }
}
