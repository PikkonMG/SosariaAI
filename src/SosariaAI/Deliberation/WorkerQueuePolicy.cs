namespace SosariaAI.Deliberation;

/// <summary>
/// Drain order for the per-provider queue. High items (player chat) go first, but a
/// streak of them cannot starve the normal channel. Asked only when a request waits.
/// </summary>
public static class WorkerQueuePolicy
{
    public const int MaxHighBeforeNormal = 3;

    public enum Pick
    {
        High,
        Normal
    }

    public static Pick Next(bool hasHigh, bool hasNormal, int highStreak) =>
        hasHigh && (highStreak < MaxHighBeforeNormal || !hasNormal) ? Pick.High : Pick.Normal;
}
