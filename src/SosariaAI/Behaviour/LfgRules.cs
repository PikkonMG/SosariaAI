using System;
using System.Collections.Generic;
using System.Text;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Memory;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>What an open group call does next.</summary>
public enum LfgCallStep
{
    /// <summary>Wait for more people to answer.</summary>
    Recruit,

    /// <summary>Enough came: gather round the leader and set out.</summary>
    Muster,

    /// <summary>Nobody came: the leader goes alone.</summary>
    GiveUp
}

/// <summary>What a running group does when its leader is off the run.</summary>
public enum RunDetour
{
    /// <summary>The leader fights for its life or mends: the group waits for it.</summary>
    Hold,

    /// <summary>An errand pulled the leader away: it drops the errand and takes up the run again.</summary>
    Restart,

    /// <summary>The leader walks home hurt: the run is over.</summary>
    End
}

/// <summary>
/// Looking for group, the 1999 way: a fighter or mage who sets out for a dungeon or a hard
/// hunt from town shouts "lfg despise anyone?", fitting people nearby answer "me" and fall
/// in, the group musters, goes, fights, and comes back, "gg all", and it breaks up. Free
/// rules. No model.
/// </summary>
public static class LfgRules
{
    /// <summary>
    /// A call carries across a bank plaza: people stepped over from the far side to answer.
    /// At speech range most calls closed with nobody answering.
    /// </summary>
    public const int AnswerRange = 30;

    /// <summary>Rolled once per dungeon trip that starts at a meeting spot.</summary>
    public const int DungeonShoutPercent = 60;

    /// <summary>Rolled once per hard hunt that starts in town.</summary>
    public const int HuntShoutPercent = 25;

    /// <summary>
    /// A hunt is hard enough to call for help when its ground is this share of the hunter's
    /// reach (<see cref="HuntGround.Reach"/>): about where an adept likes its ground best
    /// (<see cref="HuntGround.IdealShare"/>), so the upper tiers call, as before the ground
    /// was rated by its hard foe.
    /// </summary>
    public const int StrongHuntPercent = 80;

    public const int PercentScale = 100;

    public const int FriendAnswerPercent = 90;
    public const int StrangerAnswerPercent = 60;

    /// <summary>People who shared this many adventures answer each other's calls like friends.</summary>
    public const int CrewmateRuns = 2;

    /// <summary>When a leader picks whom to ask along, each shared adventure weighs like this many bond points.</summary>
    public const int SharedRunRank = 5;

    /// <summary>How a stranger ranks: below every friend, above everyone disliked.</summary>
    public const int StrangerRank = BondRules.NeutralScore;

    /// <summary>A player's own "lfg" gets at most this many offers.</summary>
    public const int PlayerCallAnswers = 2;

    public const int PlayerCallAnswerPercent = 60;

    /// <summary>A full-size wait ends with at least this many joined: a party of three.</summary>
    public const int MinJoiners = PartyScale.MinPartySize - 1;

    /// <summary>After the short-handed wait, even one joiner is enough to go as a pair.</summary>
    public const int PairJoiners = 1;

    public const int GgReplyPercent = 70;

    /// <summary>The fewest extra people a group call asks for.</summary>
    public const int MinNeeded = 1;

    /// <summary>Everyone within this many tiles of the leader counts as mustered.</summary>
    public const int MusterRange = 6;

    /// <summary>On the way and between rooms the leader waits for a member further than this.</summary>
    public const int CrewRange = 10;

    public static readonly TimeSpan ShoutRest = TimeSpan.FromMinutes(20);

    /// <summary>A fighter this close to a public moongate stands at a meeting spot.</summary>
    public const int MoongateCallTiles = 8;

    /// <summary>A fighter this close to a dungeon door, outside, stands at a meeting spot.</summary>
    public const int DoorCallTiles = 12;

    /// <summary>
    /// A fighter standing about at a bank, a moongate or a dungeon door calls a group for its
    /// own run now and then: on a shard of <see cref="PopulationPerCallLane"/> people one such
    /// call in a gap between these two, and the gap shrinks as more people are online. The
    /// bank and the gates were where groups formed.
    /// </summary>
    public static readonly TimeSpan SpotCallGapMin = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan SpotCallGapMax = TimeSpan.FromMinutes(3);

    /// <summary>Each this many people online open one more lane of spot calls.</summary>
    public const int PopulationPerCallLane = 250;

    /// <summary>The shortest gap between two spot calls, however busy the shard.</summary>
    public static readonly TimeSpan MinSpotCallGap = TimeSpan.FromSeconds(10);

    /// <summary>A fighter below this tier waits to be asked along instead of calling a group.</summary>
    public const SkillTier MinSpotCallTier = SkillTier.Apprentice;
    public static readonly TimeSpan RecruitWindow = TimeSpan.FromMinutes(2);

    /// <summary>A call short of a party of three waits this much longer before it goes as a pair.</summary>
    public static readonly TimeSpan ShortHandedWait = TimeSpan.FromMinutes(1);

    /// <summary>A guild call waits longer: guildmates recall or walk in from elsewhere.</summary>
    public static readonly TimeSpan GuildCallWindow = TimeSpan.FromMinutes(5);

    /// <summary>Friends and guildmates get the first chance; strangers answer after this.</summary>
    public static readonly TimeSpan StrangerWait = TimeSpan.FromSeconds(20);

    /// <summary>The longest the leader stands waiting for the joined to walk over.</summary>
    public static readonly TimeSpan MusterWait = TimeSpan.FromSeconds(45);

    /// <summary>The longest the leader holds at a door or between rooms for a slow member.</summary>
    public static readonly TimeSpan CrewHoldLimit = TimeSpan.FromSeconds(30);

    /// <summary>A leader off its trip this long (fled, or picked other work) has ended the run.</summary>
    public static readonly TimeSpan TripGrace = TimeSpan.FromSeconds(90);

    /// <summary>A leader that flees, heals or lies dead is waited for this long before the run ends.</summary>
    public static readonly TimeSpan HoldGrace = TimeSpan.FromMinutes(3);

    /// <summary>A leader dead this long hands the run to the next living member.</summary>
    public static readonly TimeSpan TakeOverWait = TimeSpan.FromSeconds(10);

    /// <summary>A hunt step that ends sooner than this is not a run worth a "gg".</summary>
    public static readonly TimeSpan MinTrip = TimeSpan.FromMinutes(5);

    /// <summary>
    /// A group's run goes on at least this long: a leader whose hunt ran dry or whose crawl
    /// came up early takes the run up again while it is well and stocked. Ten of 26 runs in
    /// one hour ended "the run is over" inside 15 minutes, one of them two minutes in.
    /// </summary>
    public static readonly TimeSpan MinRun = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The leader drops another job for its run at most this many times: a leader the goal
    /// loop keeps pulling away (a PK report, a walk home) has lost interest in the run.
    /// </summary>
    public const int MaxTripRestarts = 8;

    /// <summary>A call for a dungeon picks one fit for a group of this many, the fewest a full call musters.</summary>
    public const int ExpectedGroup = PartyScale.MinPartySize;

    public static readonly TimeSpan MaxTrip = TimeSpan.FromMinutes(75);

    /// <summary>
    /// A character that offered to join a player accepts that player's invite this long: long
    /// enough for a guildmate to walk across a facet to the player.
    /// </summary>
    public static readonly TimeSpan VolunteerWindow = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan PlayerCallWindow = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan PopulationRefresh = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan PruneGap = TimeSpan.FromSeconds(30);

    private const string ManEnding = "man";
    private const string MenEnding = "men";
    private const string PluralEnding = "s";
    private const string SibilantPluralEnding = "es";
    private const string ConsonantYEnding = "ies";
    private const char SingularY = 'y';
    private const string Vowels = "aeiou";
    private static readonly string[] SibilantEndings = ["s", "x", "ch", "sh"];

    /// <summary>
    /// A fighter free to lead a run: armed, healthy, not grouped, not an outlaw, and still at a
    /// meeting spot (a town, a bank, a moongate or a dungeon door).
    /// </summary>
    public static bool MayShout(bool fighter, bool armed, bool inParty, bool outlaw, bool healthy, bool atMeetingSpot) =>
        fighter && armed && !inParty && !outlaw && healthy && atMeetingSpot;

    /// <summary>One roll per trip: dungeons draw a call more often than hunts.</summary>
    public static bool RollsShout(bool dungeon, int roll100) =>
        roll100 >= 0 && roll100 < (dungeon ? DungeonShoutPercent : HuntShoutPercent);

    /// <summary>A hunt ground near the top of the hunter's reach: hard enough to want company.</summary>
    public static bool StrongHunt(int difficulty, int power) =>
        difficulty > 0 && power > 0 && difficulty * PercentScale >= HuntGround.Reach(power) * StrongHuntPercent;

    /// <summary>
    /// A called run fits a power, a lone caller's or a mustered crew's with its pets and party
    /// (<see cref="DungeonGround.FightingPower"/>), when its place lies within that power's
    /// reach (<see cref="DungeonCrawlRules.FloorFits"/>). A call picked for a crew of three that
    /// nobody answered is no run for the caller alone: 11 of 14 Destard calls and all 13 Wrong
    /// calls came from callers below the easiest hall there, and the ones who went alone died.
    /// </summary>
    public static bool RunFits(int difficulty, int power) => DungeonCrawlRules.FloorFits(difficulty, power);

    /// <summary>
    /// The power a group of <paramref name="size"/> brings, counted as a fight counts its
    /// allies: the caller's own, and the rest at <see cref="ThreatRating.PartyPowerShare"/>
    /// each, taken as like the caller, since answers must fit its power.
    /// </summary>
    public static int GroupPower(int power, int size) =>
        power + (int)(Math.Max(0, power) * Math.Max(0, size - 1) * ThreatRating.PartyPowerShare);

    /// <summary>
    /// A group's run ended early: it goes on while it is young, the leader well and stocked,
    /// and the restarts not spent.
    /// </summary>
    public static bool RunGoesOn(TimeSpan ran, bool healthy, bool suppliesLow, bool packFull, int restarts) =>
        ran < MinRun && healthy && !suppliesLow && !packFull && restarts < MaxTripRestarts;

    /// <summary>
    /// A job the leader took up is the group's run only when it is the run's own kind: a
    /// dungeon run is a dungeon trip, a hunt call a hunt. A leader pulled from its dungeon
    /// onto a hunt ended the group's run when that hunt ended.
    /// </summary>
    public static bool SameRun(bool dungeonRun, string skillKind) =>
        string.Equals(skillKind, dungeonRun ? SkillKinds.Dungeon : SkillKinds.Hunt, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A dungeon call with nobody in it who can open a gate to the door takes one who can at
    /// once: no stranger's wait and no roll. Of 26 groups in an hour, one went by gate.
    /// </summary>
    public static bool GaterWanted(bool dungeonRun, bool hasGater, bool candidateGates) =>
        dungeonRun && !hasGater && candidateGates;

    public static bool Rested(DateTime lastShout, DateTime now) => TimeRules.Rested(lastShout, now, ShoutRest);

    /// <summary>A fighter of some standing may call a group from a meeting spot.</summary>
    public static bool MayCallFromSpot(SkillTier tier) => tier >= MinSpotCallTier;

    /// <summary>
    /// The shard-wide wait before the next spot call, from the people online and a roll
    /// between zero and one: "three to five strong, scaled to how many are online".
    /// </summary>
    public static TimeSpan SpotCallGap(int population, double roll01)
    {
        var gap = RedGangRules.Between(SpotCallGapMin, SpotCallGapMax, roll01);
        var lanes = Math.Max(1, Math.Max(0, population) / PopulationPerCallLane);
        var scaled = gap / lanes;
        return scaled < MinSpotCallGap ? MinSpotCallGap : scaled;
    }

    /// <summary>Friends answer often; strangers less.</summary>
    public static bool MayAnswer(bool friend, int roll100) =>
        roll100 >= 0 && roll100 < (friend ? FriendAnswerPercent : StrangerAnswerPercent);

    /// <summary>A warm bond, or enough adventures shared, makes a crewmate. A stranger has no bond.</summary>
    public static bool IsCrewmate(Bond bond) =>
        bond != null && (BondRules.IsWarm(bond.Score) || bond.SharedCount >= CrewmateRuns);

    /// <summary>How much a leader wants this person along: the bond score plus the adventures shared.</summary>
    public static int InviteRank(Bond bond) => bond == null ? StrangerRank : bond.Score + bond.SharedCount * SharedRunRank;

    /// <summary>The index of the bond with the highest <see cref="InviteRank"/>; the first one wins a tie. Zero for an empty list.</summary>
    public static int FavoredIndex(IReadOnlyList<Bond> bonds)
    {
        var best = 0;

        for (var i = 1; i < (bonds?.Count ?? 0); i++)
        {
            if (InviteRank(bonds[i]) > InviteRank(bonds[best]))
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>Friends and guildmates get the first chance at a call.</summary>
    public static bool StrangerMustWait(bool friend, TimeSpan sinceShout) => !friend && sinceShout < StrangerWait;

    public static bool Full(int joined, int size) => joined + 1 >= size;

    /// <summary>
    /// A call fills, or waits its window for a party of three, then a little longer for a
    /// pair, then gives up.
    /// </summary>
    public static LfgCallStep NextStep(int joined, int size, TimeSpan sinceShout, TimeSpan window)
    {
        if (Full(joined, size))
        {
            return LfgCallStep.Muster;
        }

        if (sinceShout < window)
        {
            return LfgCallStep.Recruit;
        }

        if (joined >= MinJoiners)
        {
            return LfgCallStep.Muster;
        }

        if (sinceShout < window + ShortHandedWait)
        {
            return LfgCallStep.Recruit;
        }

        return joined >= PairJoiners ? LfgCallStep.Muster : LfgCallStep.GiveUp;
    }

    /// <summary>The group sets out once everyone stands close, or the muster wait is over.</summary>
    public static bool Mustered(int farthestMember, TimeSpan sinceMuster) =>
        farthestMember <= MusterRange || sinceMuster >= MusterWait;

    /// <summary>The leader holds for a member left behind, but never for long.</summary>
    public static bool HoldForCrew(int farthestMember, TimeSpan held) =>
        farthestMember > CrewRange && held < CrewHoldLimit;

    /// <summary>A call still open well past its wait: its leader stopped closing it (dead or gone).</summary>
    public static bool CallAbandoned(bool running, DateTime shoutedAt, TimeSpan window, DateTime now) =>
        !running && now - shoutedAt >= window + ShortHandedWait + MusterWait + PruneGap;

    public static bool TripCounts(DateTime runningSince, DateTime now) =>
        TimeRules.Passed(runningSince, now, MinTrip);

    public static bool TripOverdue(DateTime runningSince, DateTime now) =>
        TimeRules.Passed(runningSince, now, MaxTrip);

    /// <summary>The leader has been off the run long enough that the run is over.</summary>
    public static bool TripOver(DateTime lastOnTrip, DateTime now) =>
        TimeRules.Passed(lastOnTrip, now, TripGrace);

    /// <summary>
    /// A leader off its run. A flight, a wound being mended or a death is waited out; a
    /// hurt leader walking home or resting ends the run; any other pick of the goal loop
    /// is an errand the leader drops for the run. Three of eleven runs in one hour ended
    /// because the leader took up player conflict or a walk home.
    /// </summary>
    public static RunDetour Detour(string skillKind, bool hurt) =>
        skillKind switch
        {
            SkillKinds.Flee or SkillKinds.Heal or SkillKinds.Meditate or GhostSkill.SkillName
                or ResurrectAidSkill.SkillName => RunDetour.Hold,
            SkillKinds.GoHome or SkillKinds.Rest when hurt => RunDetour.End,
            _ => RunDetour.Restart
        };

    /// <summary>
    /// A leader put back on its run counts the restart only when another job took its place;
    /// a routine a fight or a reaction cut short (no job at all) is simply taken up again.
    /// </summary>
    public static bool CountsRestart(string skillKind) => skillKind != null;

    /// <summary>A leader held off its run by a flight or a wound this long has ended it.</summary>
    public static bool HoldOver(DateTime lastOnTrip, DateTime now) =>
        TimeRules.Passed(lastOnTrip, now, HoldGrace);

    /// <summary>The leader has lain dead long enough for the next member to lead on.</summary>
    public static bool TakeOverDue(DateTime leaderDownAt, DateTime now) =>
        TimeRules.Passed(leaderDownAt, now, TakeOverWait);

    public static bool StillVolunteering(DateTime offeredAt, DateTime now) => now - offeredAt <= VolunteerWindow;

    /// <summary>What a group call may cut short: standing about or walking somewhere, never a fight or an errand.</summary>
    public static bool Interruptible(string skillKind) =>
        skillKind is null or SkillKinds.IdleWander or SkillKinds.Loiter or SkillKinds.Rest or SkillKinds.Tavern
            or SkillKinds.Visit or SkillKinds.Sightsee or SkillKinds.BankCrowd or SkillKinds.GoTo
            or SkillKinds.Travel;

    /// <summary>
    /// Who may answer a call: someone standing about, or someone just setting out on a run of
    /// its own from town, who would rather go with company.
    /// </summary>
    public static bool FreeToJoin(string skillKind, bool ownTripStartingInTown) =>
        Interruptible(skillKind) || ownTripStartingInTown;

    /// <summary>A call always asks for at least one more.</summary>
    public static int Needed(int size) => Math.Max(MinNeeded, size - 1);

    /// <summary>A dungeon as people say it: "despise".</summary>
    public static string DungeonWord(string dungeon) =>
        string.IsNullOrWhiteSpace(dungeon) ? null : dungeon.Trim().ToLowerInvariant();

    /// <summary>
    /// A hunt ground as people say it. Inside a dungeon it is the dungeon; a generated spawn
    /// name ("GiantSpider 960-910") becomes the prey ("giant spiders"); a named place stays.
    /// </summary>
    public static string HuntWord(string groundName, string prey, string dungeon)
    {
        if (!string.IsNullOrWhiteSpace(dungeon))
        {
            return DungeonWord(dungeon);
        }

        if (string.IsNullOrWhiteSpace(groundName))
        {
            return null;
        }

        return HasDigit(groundName) ? PreyWord(prey) : groundName.Trim().ToLowerInvariant();
    }

    /// <summary>"GiantSpider" becomes "giant spiders", "Lizardman" "lizardmen", "Harpy" "harpies".</summary>
    public static string PreyWord(string prey) =>
        string.IsNullOrWhiteSpace(prey) ? null : Plural(Appraisal.SplitWords(prey.Trim()));

    private static string Plural(string word)
    {
        if (word.EndsWith(ManEnding, StringComparison.Ordinal))
        {
            return word[..^ManEnding.Length] + MenEnding;
        }

        for (var i = 0; i < SibilantEndings.Length; i++)
        {
            if (word.EndsWith(SibilantEndings[i], StringComparison.Ordinal))
            {
                return word + SibilantPluralEnding;
            }
        }

        if (word.Length > 1 && word[^1] == SingularY && !Vowels.Contains(word[^2]))
        {
            return word[..^1] + ConsonantYEnding;
        }

        return word + PluralEnding;
    }

    private static bool HasDigit(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsDigit(text[i]))
            {
                return true;
            }
        }

        return false;
    }
}
