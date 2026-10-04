using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;
using SosariaAI.Configuration;

namespace SosariaAI.Behaviour;

/// <summary>What a member of a bank crowd spends its hours doing.</summary>
public enum BankCrowdRole
{
    /// <summary>Talks trade and news, banks now and then, watches the room.</summary>
    Regular,

    /// <summary>Holds one real item and shouts what it is and what it costs.</summary>
    Hawker,

    /// <summary>Stands still; says "afk" and "back".</summary>
    Afk,

    /// <summary>Casts weak curses on itself to train Resisting Spells.</summary>
    ResistTrainer,

    /// <summary>Hides, reveals, and hides again.</summary>
    Hider,

    /// <summary>Creeps a tight ring hidden, training Stealth.</summary>
    StealthTrainer,

    /// <summary>Asks passers-by for gold, and now and then trails a real player asking.</summary>
    Beggar,

    /// <summary>A day-one player with day-one questions, trailing a real player for answers.</summary>
    Newbie
}

/// <summary>
/// The facts that decide which crowd role fits a person: temper, skills, and whether it has
/// goods to sell or reagents to burn.
/// </summary>
public readonly record struct BankCrowdCandidate(
    bool Lawful,
    bool Social,
    bool Loner,
    bool Greedy,
    bool Homebody,
    bool ResistReady,
    double Hiding,
    double StealthHidingRequirement,
    bool HasGoods,
    bool Poor,
    bool Green
);

/// <summary>
/// A T2A bank always had a crowd, and never a uniform one: a couple of regulars talking
/// trade, a hawker spamming WTS for goods it really held, an afk statue, and the macroers:
/// someone cursing himself for Resist, and now and then someone blinking in and out of
/// Hiding or creeping in circles for Stealth. Its street life came too: a beggar and a lost
/// newbie, each trailing a real player now and then. A busy town's bank holds a standing crowd of
/// up to <see cref="MaxCrowd"/>, a small town's as few as <see cref="MinCrowd"/>; members
/// come and go, the crowd stays. Pure.
/// </summary>
public static class BankCrowdRules
{
    /// <summary>The smallest standing crowd, at a bank few people call home.</summary>
    public const int MinCrowd = 3;

    /// <summary>The largest standing crowd: one person on every seat round the banker.</summary>
    public const int MaxCrowd = SeatCount;

    /// <summary>Live people who call a bank home for each place in its crowd.</summary>
    public const int ResidentsPerMember = 20;

    /// <summary>Minutes between counts of the people who call each bank home.</summary>
    public const int ResidentRefreshMinutes = 5;

    /// <summary>A crowd this big has room for a second hawker and a second afk statue.</summary>
    public const int BusyCrowd = 6;

    public const int MinRegulars = 2;

    /// <summary>About one seat in three is a regular talking trade.</summary>
    public const int SeatsPerRegular = 3;

    public const int MaxPerRole = 1;
    public const int BusyMaxPerRole = 2;

    /// <summary>Hiders and stealth trainers are one person at a time, and never the first to arrive.</summary>
    public const int HiddenCap = 1;

    public const int VisibleBeforeHidden = 2;

    /// <summary>The role a bank drafted from a Banker spawner carries; a named bank is preferred over it.</summary>
    public const string GeneratedBankerRole = "Banker";

    /// <summary>Below this Hiding a hider fails nearly every try.</summary>
    public const double HiderMinHiding = 30;

    public const int MinHoldMinutes = 20;
    public const int MaxHoldMinutes = 45;
    public const double MinHoldScale = 0.5;
    public const double MaxHoldScale = 2.0;

    public const int RegularChatMinSeconds = 50;
    public const int RegularChatMaxSeconds = 110;
    public const int HawkerShoutMinSeconds = 45;
    public const int HawkerShoutMaxSeconds = 90;

    /// <summary>Seconds between a resist trainer's casts: the chant, the aim, a breath.</summary>
    public const int ResistCastGapSeconds = 5;

    /// <summary>Seconds a resist trainer sits meditating when its mana runs dry.</summary>
    public const int MeditateSeconds = 20;

    /// <summary>Out of a hundred, how often a regular looks into its bank box between chats.</summary>
    public const int RegularBoxCheckPercent = 15;

    /// <summary>Out of a hundred, how often an afk statue says so, and says it is back.</summary>
    public const int AfkLinePercent = 40;

    /// <summary>A poor person holding less than this, pack and bank together, may beg at the bank.</summary>
    public const int BeggarGoldCeiling = 300;

    /// <summary>A beggar or a newbie notices a real player this close.</summary>
    public const int StreetNoticeTiles = 8;

    /// <summary>A trailing beggar or newbie keeps this close behind the player.</summary>
    public const int StreetTrailTiles = 2;

    /// <summary>Seconds a beggar or a newbie trails one player before it gives up.</summary>
    public const int StreetTrailSeconds = 25;

    /// <summary>Minutes after giving up before it trails anyone again.</summary>
    public const int StreetTrailRestMinutes = 4;

    /// <summary>Out of a hundred, how often a look round for a player ends with trailing one.</summary>
    public const int StreetTrailPercent = 15;

    /// <summary>Out of a hundred, how often a beggar or a newbie says its line when its turn comes.</summary>
    public const int StreetChatterPercent = 60;

    public const int StreetChatterMinSeconds = 20;
    public const int StreetChatterMaxSeconds = 70;

    public const int PercentScale = 100;

    public const string AfkLine = "afk";
    public const string BackLine = "back";

    /// <summary>A member farther than this from its bank has left the crowd.</summary>
    public const int LeaveRange = BankPlaza.Range;

    /// <summary>Seats around a bank's arrival point, so the crowd never stands on one tile.</summary>
    public const int SeatCount = 8;

    public const int SeatRadius = 3;
    public const int SeatArrivalRange = 1;
    public const int NoSeat = -1;

    /// <summary>Minutes a new member has to walk to its bank before its place goes to someone else.</summary>
    public const int WalkGraceMinutes = 10;

    /// <summary>
    /// Seconds a seat stays promised to the person who chose the crowd, until its job begins
    /// and claims it. At boot a job began up to nine seconds after the choice.
    /// </summary>
    public const int PromiseSeconds = 15;

    private const int InclusiveSpanPad = 1;

    public static readonly TimeSpan PromiseHold = TimeSpan.FromSeconds(PromiseSeconds);

    public static readonly TimeSpan LongestHold = TimeSpan.FromMinutes(MaxHoldMinutes * MaxHoldScale);

    public static readonly TimeSpan WalkGrace = TimeSpan.FromMinutes(WalkGraceMinutes);

    private static readonly (int X, int Y)[] SeatOffsets =
    [
        (SeatRadius, 0),
        (-SeatRadius, 0),
        (0, SeatRadius),
        (0, -SeatRadius),
        (SeatRadius, SeatRadius),
        (-SeatRadius, -SeatRadius),
        (SeatRadius, -SeatRadius),
        (-SeatRadius, SeatRadius)
    ];

    public static readonly TimeSpan ResidentRefresh = TimeSpan.FromMinutes(ResidentRefreshMinutes);

    /// <summary>The order open roles are filled when a person has no leaning of its own.</summary>
    private static readonly BankCrowdRole[] FillOrder =
    [
        BankCrowdRole.Regular,
        BankCrowdRole.Hawker,
        BankCrowdRole.ResistTrainer,
        BankCrowdRole.Afk,
        BankCrowdRole.Hider,
        BankCrowdRole.StealthTrainer
    ];

    /// <summary>The standing crowd a bank holds for the live people who call it home.</summary>
    public static int TargetFor(int residents) =>
        Math.Clamp((Math.Max(0, residents) + ResidentsPerMember - 1) / ResidentsPerMember, MinCrowd, MaxCrowd);

    /// <summary>
    /// True when a crowd of <paramref name="target"/> has a seat left over the seats held and
    /// the seats promised to people whose crowd job has not begun yet.
    /// </summary>
    public static bool HasRoom(int seated, int promised, int target) =>
        Math.Max(0, seated) + Math.Max(0, promised) < target;

    /// <summary>True while a seat promised until <paramref name="until"/> still holds.</summary>
    public static bool PromiseHolds(DateTime until, DateTime now) => now < until;

    /// <summary>How many of a role a crowd of <paramref name="target"/> holds at once.</summary>
    public static int Cap(BankCrowdRole role, int target) =>
        role switch
        {
            BankCrowdRole.Regular => Math.Max(MinRegulars, (target + 1) / SeatsPerRegular),
            BankCrowdRole.Hawker or BankCrowdRole.Afk => target >= BusyCrowd ? BusyMaxPerRole : MaxPerRole,
            _ => MaxPerRole
        };

    public static bool IsHidden(BankCrowdRole role) => role is BankCrowdRole.Hider or BankCrowdRole.StealthTrainer;

    public static int Count(IReadOnlyList<BankCrowdRole> seated, BankCrowdRole role)
    {
        var count = 0;

        for (var i = 0; i < (seated?.Count ?? 0); i++)
        {
            if (seated[i] == role)
            {
                count++;
            }
        }

        return count;
    }

    public static int HiddenCount(IReadOnlyList<BankCrowdRole> seated) =>
        Count(seated, BankCrowdRole.Hider) + Count(seated, BankCrowdRole.StealthTrainer);

    /// <summary>
    /// True when a crowd of <paramref name="target"/> holding <paramref name="seated"/> has
    /// room for one more of <paramref name="role"/>. A hidden role waits for a visible crowd:
    /// a bank of people nobody can see is an empty bank.
    /// </summary>
    public static bool IsOpen(IReadOnlyList<BankCrowdRole> seated, BankCrowdRole role, int target)
    {
        var count = seated?.Count ?? 0;

        if (count >= target || Count(seated, role) >= Cap(role, target))
        {
            return false;
        }

        var hidden = HiddenCount(seated);
        return !IsHidden(role) || hidden < HiddenCap && count - hidden >= VisibleBeforeHidden;
    }

    public static bool Fits(BankCrowdRole role, BankCrowdCandidate candidate)
    {
        if (!candidate.Lawful)
        {
            return false;
        }

        return role switch
        {
            BankCrowdRole.Hawker => candidate.HasGoods,
            BankCrowdRole.ResistTrainer => candidate.ResistReady,
            BankCrowdRole.Hider => candidate.Hiding >= HiderMinHiding,
            BankCrowdRole.StealthTrainer => candidate.Hiding >= candidate.StealthHidingRequirement,
            BankCrowdRole.Beggar => candidate.Poor,
            BankCrowdRole.Newbie => candidate.Green,
            _ => true
        };
    }

    /// <summary>
    /// The role this person takes in a crowd already holding <paramref name="seated"/>, or
    /// null when the crowd is full or no open role fits. A person with the skills trains; a
    /// greedy one with goods hawks; a social one talks; a loner stands afk.
    /// </summary>
    public static BankCrowdRole? Choose(IReadOnlyList<BankCrowdRole> seated, BankCrowdCandidate candidate, int target)
    {
        if (!candidate.Lawful || (seated?.Count ?? 0) >= target)
        {
            return null;
        }

        foreach (var role in Leanings(candidate))
        {
            if (IsOpen(seated, role, target) && Fits(role, candidate))
            {
                return role;
            }
        }

        for (var i = 0; i < FillOrder.Length; i++)
        {
            if (IsOpen(seated, FillOrder[i], target) && Fits(FillOrder[i], candidate))
            {
                return FillOrder[i];
            }
        }

        return null;
    }

    /// <summary>
    /// The roles a person leans to, strongest first. The hidden roles come last: a skilled
    /// hider who is also social talks trade first. Half the old crowd's joins were hiders.
    /// A poor person begs and a green one asks the way before it talks trade; each bank
    /// holds one of each at most, so the street life stays a face or two.
    /// </summary>
    public static IEnumerable<BankCrowdRole> Leanings(BankCrowdCandidate candidate)
    {
        if (candidate.ResistReady)
        {
            yield return BankCrowdRole.ResistTrainer;
        }

        if (candidate.Greedy && candidate.HasGoods)
        {
            yield return BankCrowdRole.Hawker;
        }

        if (candidate.Poor)
        {
            yield return BankCrowdRole.Beggar;
        }

        if (candidate.Green)
        {
            yield return BankCrowdRole.Newbie;
        }

        if (candidate.Social)
        {
            yield return BankCrowdRole.Regular;
        }

        if (candidate.Loner || candidate.Homebody)
        {
            yield return BankCrowdRole.Afk;
        }

        if (candidate.Hiding >= candidate.StealthHidingRequirement)
        {
            yield return BankCrowdRole.StealthTrainer;
        }

        if (candidate.Hiding >= HiderMinHiding)
        {
            yield return BankCrowdRole.Hider;
        }
    }

    /// <summary>
    /// The bank a crowd gathers at. Two Banker spawners can stand in one hall, and Magincia
    /// had two crowds twelve tiles apart. Every bank within <see cref="LeaveRange"/> of the
    /// nearest one is the same bank: a named bank wins over one drafted from a spawner, then
    /// the first name.
    /// </summary>
    public static Destination Canonical(IReadOnlyList<Destination> banks, Destination nearest)
    {
        var best = nearest;

        for (var i = 0; i < (banks?.Count ?? 0) && nearest != null; i++)
        {
            var bank = banks[i];

            if (bank != null && AtBank(bank.Arrival, nearest.Arrival) && Prefer(bank, best))
            {
                best = bank;
            }
        }

        return best;
    }

    /// <summary>How long a member holds its place: a long visible stretch, longer for a homebody.</summary>
    public static TimeSpan HoldLength(int roll, double phaseMultiplier) =>
        TimeSpan.FromMinutes(
            Span(roll, MinHoldMinutes, MaxHoldMinutes) * Math.Clamp(phaseMultiplier, MinHoldScale, MaxHoldScale)
        );

    public static TimeSpan ChatGap(int roll) =>
        TimeSpan.FromSeconds(Span(roll, RegularChatMinSeconds, RegularChatMaxSeconds));

    public static TimeSpan ShoutGap(int roll) =>
        TimeSpan.FromSeconds(Span(roll, HawkerShoutMinSeconds, HawkerShoutMaxSeconds));

    public static TimeSpan StreetChatterGap(int roll) =>
        TimeSpan.FromSeconds(Span(roll, StreetChatterMinSeconds, StreetChatterMaxSeconds));

    public static readonly TimeSpan StreetTrail = TimeSpan.FromSeconds(StreetTrailSeconds);

    public static readonly TimeSpan StreetTrailRest = TimeSpan.FromMinutes(StreetTrailRestMinutes);

    /// <summary>
    /// A trailing beggar or newbie gives up when the player walks so far from the bank that
    /// trailing would take it off the bank floor, goes out of reach, or the trail ran its
    /// time.
    /// </summary>
    public static bool StopsTrailing(int playerTilesFromBank, int tilesToPlayer, TimeSpan trailed) =>
        playerTilesFromBank > LeaveRange - StreetTrailTiles ||
        tilesToPlayer > StreetNoticeTiles + StreetTrailTiles ||
        trailed >= StreetTrail;

    public static bool Chance(int roll, int percent) => Math.Abs(roll % PercentScale) < percent;

    /// <summary>The lowest seat not in <paramref name="taken"/>, or <see cref="NoSeat"/> when all are taken.</summary>
    public static int FreeSeat(IReadOnlyCollection<int> taken)
    {
        for (var seat = 0; seat < SeatCount; seat++)
        {
            if (taken == null || !Contains(taken, seat))
            {
                return seat;
            }
        }

        return NoSeat;
    }

    public static Point3D SeatSpot(Point3D bank, int seat)
    {
        var (x, y) = SeatOffsets[Math.Abs(seat) % SeatOffsets.Length];
        return new Point3D(bank.X + x, bank.Y + y, bank.Z);
    }

    public static bool AtBank(Point3D at, Point3D bank) => NavMetric.Chebyshev(at, bank) <= LeaveRange;

    /// <summary>
    /// A member has left the crowd when it stands away from the bank after arriving, never
    /// arrived within the walk grace, or held its place past the longest hold.
    /// </summary>
    public static bool HasLeft(bool arrived, bool atBank, TimeSpan held) =>
        held > LongestHold || (arrived ? !atBank : held > WalkGrace);

    private static bool Prefer(Destination candidate, Destination current)
    {
        var generated = IsGenerated(candidate);

        if (generated != IsGenerated(current))
        {
            return !generated;
        }

        return string.CompareOrdinal(candidate.Name, current.Name) < 0;
    }

    private static bool IsGenerated(Destination bank) =>
        string.Equals(bank.Role, GeneratedBankerRole, StringComparison.Ordinal);

    private static int Span(int roll, int min, int max) => min + Math.Abs(roll % (max - min + InclusiveSpanPad));

    private static bool Contains(IReadOnlyCollection<int> taken, int seat)
    {
        foreach (var value in taken)
        {
            if (value == seat)
            {
                return true;
            }
        }

        return false;
    }
}
