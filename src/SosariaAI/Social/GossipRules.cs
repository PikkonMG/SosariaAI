using System;
using SosariaAI.Common;

namespace SosariaAI.Social;

public static class GossipRules
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(3);
    public static readonly TimeSpan MinNewsAge = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan VividAge = TimeSpan.FromMinutes(45);
    public static readonly TimeSpan RetellAfter = TimeSpan.FromMinutes(15);
    public const double NewsSecondsPerTile = 2.0;
    public const int HereRadius = 40;
    public const int VividRadius = 400;
    public const int VagueRadius = 1200;
    public const int RepeatKillerAt = 3;
    public const int JournalCapacity = 300;

    /// <summary>A story told this many times shard-wide is worn out: everyone has heard it.</summary>
    public const int MaxTells = 6;

    /// <summary>Detail levels: faded news is not told, vague news loses its time and killer.</summary>
    public const int FadedDetail = 0;
    public const int VagueDetail = 1;
    public const int VividDetail = 2;

    /// <summary>Vague news from far away is retold this often; vivid news always.</summary>
    public const int VagueRetellPercent = 40;
    public const int VividRetellPercent = 100;

    /// <summary>Words for how long ago it happened.</summary>
    public static readonly TimeSpan JustNowAge = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan BitAgoAge = TimeSpan.FromMinutes(40);
    public static readonly TimeSpan HourAgoAge = TimeSpan.FromMinutes(90);

    /// <summary>
    /// Nobody reports the spot they stand on: the people there saw it. This holds for the
    /// teller's own story too, and for any age.
    /// </summary>
    public static bool TooLocal(int chebyshev) => chebyshev <= HereRadius;

    public static bool NewsHasArrived(int chebyshev, TimeSpan age, bool ownEvent)
    {
        if (ownEvent)
        {
            return age >= TimeSpan.Zero;
        }

        if (age < MinNewsAge)
        {
            return false;
        }

        var travel = TimeSpan.FromSeconds(chebyshev * NewsSecondsPerTile);
        return age >= travel;
    }

    public static int DetailLevel(int chebyshev, TimeSpan age, bool ownEvent)
    {
        if (ownEvent || (chebyshev <= VividRadius && age < VividAge))
        {
            return VividDetail;
        }

        if (chebyshev <= VagueRadius || age < VividAge)
        {
            return VagueDetail;
        }

        return FadedDetail;
    }

    /// <summary>News fades with distance: vague news is retold less often, faded news never.</summary>
    public static bool Retold(int detailLevel, int roll100) =>
        detailLevel switch
        {
            VividDetail => roll100 < VividRetellPercent,
            VagueDetail => roll100 < VagueRetellPercent,
            _ => false
        };

    /// <summary>
    /// A red sighting is a warning told once; any other story rests between tellings and is
    /// worn out after <see cref="MaxTells"/>. Each teller tells it once (see <see cref="ShardEvent.ToldBy"/>).
    /// </summary>
    public static bool MayTell(string type, int tellCount, DateTime lastToldAt, DateTime now) =>
        (type != ShardEventType.Red || tellCount == 0) && tellCount < MaxTells &&
        TimeRules.Rested(lastToldAt, now, RetellAfter);

    public static bool IsRepeatKiller(int kills) => kills >= RepeatKillerAt;

    /// <summary>A thief or a red does not spread news of its own crime, nor a red of its own fall.</summary>
    public static bool KeepsQuiet(string type, bool tellerIsActor) =>
        tellerIsActor && type is ShardEventType.Theft or ShardEventType.Red or ShardEventType.RedKill;

    public static double Weight(string type, int tellCount, bool ownStory)
    {
        var baseWeight = type switch
        {
            ShardEventType.Pk => 4.0,
            ShardEventType.Death => 3.0,
            ShardEventType.Party => 1.5,
            ShardEventType.Theft => 2.0,
            ShardEventType.Red => 0.8,
            ShardEventType.GuildWar => 3.5,
            ShardEventType.Treasure => 2.0,
            ShardEventType.Duel => 2.0,
            ShardEventType.RedKill => 3.0,
            ShardEventType.SeaFind => 1.5,
            _ => 1.0
        };

        var faded = baseWeight / (1 + Math.Max(0, tellCount));
        return ownStory ? faded * 2.5 : faded;
    }

    /// <summary>How a 1999 player says when: never a clock time.</summary>
    public static string WhenWord(TimeSpan age) =>
        age < JustNowAge ? "just now" :
        age < BitAgoAge ? "a bit ago" :
        age < HourAgoAge ? "like an hour ago" :
        "earlier";
}
