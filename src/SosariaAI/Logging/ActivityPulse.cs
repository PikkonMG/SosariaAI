using System;
using System.Threading;
using Server;
using Server.Logging;
using SosariaAI.Mobiles;

namespace SosariaAI.Logging;

/// <summary>
/// One console line a minute about the whole population, in place of a hundred
/// characters' lines a second. The detail stays in the activity file.
/// </summary>
public static class ActivityPulse
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>Raised after the minute line, so other minute work rides this one timer.</summary>
    public static event Action MinutePassed;

    private static readonly ILogger console = LogFactory.GetLogger(typeof(ActivityPulse));
    private static int _done;
    private static int _failed;
    private static int _noRoute;
    private static int _gaveUp;
    private static bool _started;

    public static void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        Server.Timer.DelayCall(Interval, Interval, Print);
    }

    public static void NoteDone() => Interlocked.Increment(ref _done);

    public static void NoteFailed() => Interlocked.Increment(ref _failed);

    public static void NoteNoRoute() => Interlocked.Increment(ref _noRoute);

    public static void NoteGaveUp() => Interlocked.Increment(ref _gaveUp);

    public static string Line(int people, int done, int failed, int noRoute, int gaveUp, string path) =>
        $"SosariaAI last minute: {people} people, {done} steps done, {failed} failed, " +
        $"{noRoute} without a route, {gaveUp} plans given up. Detail: {path}";

    private static void Print()
    {
        var people = 0;

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is SosariaCharacter { Deleted: false } character && People.InWorld(character))
            {
                people++;
            }
        }

        console.Information(
            Line(
                people,
                Interlocked.Exchange(ref _done, 0),
                Interlocked.Exchange(ref _failed, 0),
                Interlocked.Exchange(ref _noRoute, 0),
                Interlocked.Exchange(ref _gaveUp, 0),
                ActivityFile.Path
            )
        );
        MinutePassed?.Invoke();
    }
}
