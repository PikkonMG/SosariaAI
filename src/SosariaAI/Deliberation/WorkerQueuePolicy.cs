namespace SosariaAI.Deliberation;

/// <summary>
/// Drain order for the per-provider queue. High items (player chat) go first, but a
/// streak of them cannot starve the normal channel.
/// </summary>
public static class WorkerQueuePolicy
{
    public const int MaxHighBeforeNormal = 3;

    public enum Pick
    {
        None,
        High,
        Normal
    }

    public static Pick Next(bool hasHigh, bool hasNormal, int highStreak)
    {
        if (hasHigh && highStreak < MaxHighBeforeNormal)
        {
            return Pick.High;
        }

        if (hasNormal)
        {
            return Pick.Normal;
        }

        if (hasHigh)
        {
            return Pick.High;
        }

        return Pick.None;
    }
}
