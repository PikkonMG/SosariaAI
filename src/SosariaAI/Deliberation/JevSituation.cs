using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;

namespace SosariaAI.Deliberation;

/// <summary>
/// The few facts a Jev decision needs about one character, read on the world thread. The
/// numbers stay here; <see cref="JevSituationState"/> turns them into named words, because
/// Jev judges words better than magnitudes.
/// </summary>
public sealed record JevSituation
{
    /// <summary>Tier and class in a few words, from <see cref="IdentityLine.Brief"/>.</summary>
    public string Identity { get; init; }

    public PersonaDrives Drives { get; init; } = PersonaDrives.Neutral;

    public string Want { get; init; }

    public string Mood { get; init; }

    public string DayPart { get; init; }

    public string Place { get; init; }

    public bool InTown { get; init; }

    public int DistanceFromHome { get; init; }

    public int Hits { get; init; }

    public int HitsMax { get; init; }

    public int Mana { get; init; }

    public int ManaMax { get; init; }

    /// <summary>Carried and banked gold together.</summary>
    public int Gold { get; init; }

    /// <summary>Pack weight over what the character can carry, 0 to 1 and over.</summary>
    public double PackFill { get; init; }

    public bool GoodsToSell { get; init; }

    public TimeSpan SinceHunt { get; init; } = TimeSpan.MaxValue;

    public TimeSpan SinceRest { get; init; } = TimeSpan.MaxValue;

    public TimeSpan SinceBank { get; init; } = TimeSpan.MaxValue;

    public bool ThreatInSight { get; init; }

    public int RecentRuns { get; init; }

    public bool DiedRecently { get; init; }

    /// <summary>Party member names, or null when alone.</summary>
    public string Party { get; init; }

    public bool PartyForming { get; init; }

    /// <summary>What the character is doing, or what it just finished, as a plain phrase.</summary>
    public string Doing { get; init; }

    /// <summary>Why the job plan's next step runs, as a plain phrase; null without a plan.</summary>
    public string Plan { get; init; }

    /// <summary>The big moment that put this pick to Jev, in a few words; null for a routine pick.</summary>
    public string Moment { get; init; }

    /// <summary>A red: the red words ride along (armed, gang at the Den, blue bands out).</summary>
    public bool Red { get; init; }

    /// <summary>The combat kit and a weapon to hold (see <see cref="SosariaAI.Skills.SpareKit.Armed"/>).</summary>
    public bool Armed { get; init; } = true;

    /// <summary>Living gang mates standing in Buccaneer's Den now.</summary>
    public int GangAtDen { get; init; }

    /// <summary>The anti-PK band riding near this red or the camp of its run, or null for none.</summary>
    public RoadGroupKind? BlueBand { get; init; }
}

/// <summary>
/// Turns a <see cref="JevSituation"/> into the small, named, word-only state Jev reads.
/// Each field is one short phrase, since every input token is paid for; memory lines are
/// left out for the same reason. Pure. Every bucket boundary is a named constant.
/// </summary>
public static class JevSituationState
{
    public const double DriveLow = 0.34;
    public const double DriveHigh = 0.66;

    public const double HitsScratched = 0.95;
    public const double HitsWounded = 0.7;
    public const double HitsBadlyWounded = 0.4;
    public const double HitsNearDeath = 0.15;

    public const double ManaFull = 0.9;
    public const double ManaHalf = 0.5;
    public const double ManaLow = 0.2;

    public const int GoldPoor = 50;
    public const int GoldComfortable = 500;
    public const int GoldRich = 5000;

    public const double PackLight = 0.3;
    public const double PackOverloaded = 0.95;

    public const int HomeNear = 100;
    public const int HomeFar = 500;

    public static readonly TimeSpan JustNow = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan WithinTheHour = TimeSpan.FromHours(1);
    public static readonly TimeSpan HoursAgo = TimeSpan.FromHours(6);

    internal const string Never = "never";
    internal const string None = "none";

    public static Dictionary<string, object> Build(JevSituation situation)
    {
        situation ??= new JevSituation();
        var state = new Dictionary<string, object>
        {
            ["who"] = string.IsNullOrWhiteSpace(situation.Identity) ? "a person" : situation.Identity,
            ["temper"] = TemperWords(situation.Drives),
            ["want"] = WantWords(situation.Want, situation.Mood),
            ["time"] = string.IsNullOrWhiteSpace(situation.DayPart) ? "day" : situation.DayPart.ToLowerInvariant(),
            ["place"] = PlaceWords(situation.Place, situation.InTown, situation.DistanceFromHome),
            ["body"] = $"{HitsWord(situation.Hits, situation.HitsMax)}, mana {ManaWord(situation.Mana, situation.ManaMax)}",
            ["goods"] =
                $"purse {GoldWord(situation.Gold)}, pack {PackWord(situation.PackFill)}, {(situation.GoodsToSell ? "goods to sell" : "nothing to sell")}",
            ["last"] =
                $"hunt {SinceWord(situation.SinceHunt)}, rest {SinceWord(situation.SinceRest)}, bank {SinceWord(situation.SinceBank)}",
            ["danger"] = DangerWords(situation.ThreatInSight, situation.RecentRuns, situation.DiedRecently),
            ["party"] = PartyWords(situation.Party, situation.PartyForming),
            ["doing"] = string.IsNullOrWhiteSpace(situation.Doing) ? ActionDescriptions.Idle : situation.Doing
        };

        if (!string.IsNullOrWhiteSpace(situation.Plan))
        {
            state["plan"] = situation.Plan;
        }

        if (!string.IsNullOrWhiteSpace(situation.Moment))
        {
            state["moment"] = situation.Moment;
        }

        if (situation.Red)
        {
            state["red"] = RedWords(situation.Armed, situation.GangAtDen, situation.BlueBand);
        }

        return state;
    }

    /// <summary>"armed, several gang mates at the Den, blues sweep near your camp".</summary>
    public static string RedWords(bool armed, int gangAtDen, RoadGroupKind? blueBand) =>
        $"{RedMomentJev.ArmedWord(armed)}, {CombatStanceJev.Counted(gangAtDen, "gang mate", "gang mates")} at the Den, " +
        BlueBandWords(blueBand);

    public static string BlueBandWords(RoadGroupKind? band) =>
        band switch
        {
            RoadGroupKind.DenRaid => "blues raid the Den",
            RoadGroupKind.Sweep => "blues sweep near your camp",
            _ => "no blue band out"
        };

    /// <summary>Only the drives that stand out; a middling person is even-tempered.</summary>
    public static string TemperWords(PersonaDrives drives)
    {
        drives ??= PersonaDrives.Neutral;
        var words = new List<string>();
        AddDrive(words, drives.Greed, "greedy", "content");
        AddDrive(words, drives.Caution, "careful", "reckless");
        AddDrive(words, drives.Valor, "brave", "timid");
        return words.Count == 0 ? "even-tempered" : string.Join(", ", words);
    }

    public static string HitsWord(int hits, int hitsMax)
    {
        var fraction = hitsMax <= 0 ? 1.0 : (double)hits / hitsMax;

        return fraction switch
        {
            >= HitsScratched => "unhurt",
            >= HitsWounded => "scratched",
            >= HitsBadlyWounded => "wounded",
            >= HitsNearDeath => "badly wounded",
            _ => "near death"
        };
    }

    public static string ManaWord(int mana, int manaMax)
    {
        var fraction = manaMax <= 0 ? 1.0 : (double)mana / manaMax;

        return fraction switch
        {
            >= ManaFull => "full",
            >= ManaHalf => "half",
            >= ManaLow => "low",
            _ => "empty"
        };
    }

    public static string GoldWord(int gold) => gold switch
    {
        < GoldPoor => "broke",
        < GoldComfortable => "poor",
        < GoldRich => "comfortable",
        _ => "rich"
    };

    public static string PackWord(double fill) => fill switch
    {
        < PackLight => "light",
        < NeedsBrain.PackedFraction => "half full",
        < PackOverloaded => "heavy",
        _ => "overloaded"
    };

    public static string HomeWord(int tiles) => tiles switch
    {
        <= 0 => "at home",
        <= HomeNear => "near home",
        <= HomeFar => "a walk from home",
        _ => "far from home"
    };

    public static string SinceWord(TimeSpan since)
    {
        if (since == TimeSpan.MaxValue)
        {
            return Never;
        }

        if (since < JustNow)
        {
            return "just now";
        }

        if (since < WithinTheHour)
        {
            return "within the hour";
        }

        return since < HoursAgo ? "hours ago" : "long ago";
    }

    public static string DangerWords(bool threatInSight, int recentRuns, bool diedRecently)
    {
        var words = new List<string>();

        if (threatInSight)
        {
            words.Add("a threat in sight");
        }

        if (recentRuns > 0)
        {
            words.Add("fled danger lately");
        }

        if (diedRecently)
        {
            words.Add("died lately");
        }

        return words.Count == 0 ? None : string.Join(", ", words);
    }

    public static string PartyWords(string members, bool forming)
    {
        if (forming)
        {
            return "a party is forming";
        }

        return string.IsNullOrWhiteSpace(members) || members.Equals(None, StringComparison.OrdinalIgnoreCase)
            ? "alone"
            : $"in a party with {members}";
    }

    private static string WantWords(string want, string mood)
    {
        if (string.IsNullOrWhiteSpace(want))
        {
            return None;
        }

        return string.IsNullOrWhiteSpace(mood) ? want : $"{want} ({mood})";
    }

    private static string PlaceWords(string place, bool inTown, int distanceFromHome)
    {
        var name = string.IsNullOrWhiteSpace(place) ? "Sosaria" : place;
        return $"{name}, {(inTown ? "in town" : "outside town")}, {HomeWord(distanceFromHome)}";
    }

    private static void AddDrive(List<string> words, double value, string high, string low)
    {
        if (value >= DriveHigh)
        {
            words.Add(high);
        }
        else if (value <= DriveLow)
        {
            words.Add(low);
        }
    }
}
