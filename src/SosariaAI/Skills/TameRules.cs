using System;
using System.Collections.Generic;
using SosariaAI.Combat;

namespace SosariaAI.Skills;

/// <summary>
/// What the engine tells a tamer about one kind of beast, read off a live wild one: the
/// skill its cursor asks, the follower slots it takes, how hard it fights (the
/// <see cref="ThreatRating"/> score of its full hits, strength and blow), and who may tame it.
/// </summary>
public readonly record struct BeastProfile(
    string Kind,
    double MinTameSkill,
    int ControlSlots,
    int Power,
    bool AllowFemaleTamer,
    bool AllowMaleTamer,
    bool SubdueBeforeTame
);

/// <summary>
/// The tamer's side of the engine's checks: its taming, its sex and its free follower slots,
/// whether it practises now (its taming still gains by the engine's rules and its practice
/// session is open, <see cref="TamePractice"/>), and the power of the fighting pet it already
/// owns, out or in the stables (zero with none).
/// </summary>
public readonly record struct TamerFacts(double Taming, bool Female, int Followers, int FollowersMax, bool Practises, int FighterPower = 0);

/// <summary>A pet a tamer owns: how hard it fights, and whether it can be ridden.</summary>
public readonly record struct OwnedPet(int Power, bool IsMount);

/// <summary>
/// A 1999 mage-tamer's judgement, on the engine's own numbers. The engine refuses a beast the
/// tamer's skill does not reach (<c>MinTameSkill</c>), one its follower slots cannot hold,
/// one of the wrong sex for it, one that must be beaten first and one with too many owners.
/// Past the gate its check rolls the tamer's skill against the beast's minimum plus 24.9, in a
/// window of 25 each way, six points harder for every earlier owner; a pet it controls answers
/// by the engine's control chance. A tamer keeps only a beast strong for its tier
/// (<see cref="KeepPower"/>): a master rides out with drakes and dragons, a grandmaster with
/// nightmares and white wyrms, and nobody keeps a bull, a black bear or a giant spider. It
/// keeps one fighting pet and, while it rides none, one mount (<see cref="Keeps"/>); it goes
/// for another fighter only when that one is far stronger (<see cref="UpgradeFactor"/>) and
/// lets the old one go: in 77 minutes tamers kept 17 drakes in Destard, one tamer two in three
/// minutes, and left the pets by its door. It practises only while its taming still gains,
/// on a beast hard enough to teach (<see cref="MaxPracticeOdds"/>), and from Expert on never
/// on the wolves, cats and harts of the forest (<see cref="ExpertPracticePower"/>); a beast below <see cref="LowBeastPower"/>
/// (cows, chickens, sheep, pigs, goats, dogs, rabbits, birds and rats) is never tamed at all.
/// In one night 163 tamers tamed and let go 1,145 beasts and kept 30 bulls and 20 black bears.
/// </summary>
public static class TameRules
{
    /// <summary>The engine's taming check adds this to the beast's minimum (Skills/AnimalTaming.cs).</summary>
    public const double EngineMinSkillBias = 24.9;

    /// <summary>The engine's taming check runs this far either side of the biased minimum.</summary>
    public const double EngineCheckWindow = 25.0;

    /// <summary>Each owner a beast had before makes the engine's check this much harder.</summary>
    public const double EngineOwnerPenalty = 6.0;

    /// <summary>A beast with this many owners is "too upset" for a new one (BaseCreature.MaxOwners).</summary>
    public const int EngineMaxOwners = 5;

    /// <summary>
    /// A trip is made only for odds this good on each try. A grandmaster has one in eight on a
    /// dragon and one in ten on a nightmare; a master stays with drakes, as players did.
    /// </summary>
    public const double MinTameOdds = 0.075;

    /// <summary>A pet that disobeys more than three orders in ten is no use in a fight.</summary>
    public const double MinControlChance = 0.7;

    /// <summary>
    /// A tamer below Expert keeps a beast that fights at least this hard: a polar or grizzly
    /// bear (about 145 and 160 on the engine's pre-AOS hits), a dire wolf (128). A bull, a
    /// black or brown bear, a giant spider, a scorpion or a gator fights about 100.
    /// </summary>
    public const int NoviceKeepPower = 125;

    /// <summary>An Expert keeps a grizzly, a polar bear or better.</summary>
    public const int ExpertKeepPower = 140;

    /// <summary>An Adept keeps a hell hound, a lava lizard, a grizzly or a drake.</summary>
    public const int AdeptKeepPower = 150;

    /// <summary>A master or a grandmaster keeps drakes (about 450), nightmares, dragons and white wyrms.</summary>
    public const int MasterKeepPower = 300;

    /// <summary>
    /// Town and farm beasts fight below this on the engine's pre-AOS hits: a cow scores 34, a
    /// sheep 22, a pig or a goat 24, a cat 11, a chicken 6, a dog 40, a llama 41. An eagle, a
    /// wolf, a cougar or a bear is the first wild beast above it.
    /// </summary>
    public const int LowBeastPower = 45;

    /// <summary>
    /// From Expert on a tamer practises only on a beast this strong: bulls, grizzlies, giant
    /// spiders and toads, dire wolves and drakes, never the wolves, leopards, cougars, harts and
    /// eagles of the forest (about 50 to 85 on the engine's pre-AOS hits).
    /// </summary>
    public const int ExpertPracticePower = 90;

    /// <summary>
    /// A practice beast must be this hard at the most: an even chance on each try. The engine's
    /// gain chance grows as the odds fall, so a near sure tame teaches almost nothing.
    /// </summary>
    public const double MaxPracticeOdds = 0.5;

    /// <summary>A practice session lets this many beasts go, then the tamer turns to other work.</summary>
    public const int PracticeTamesPerSession = 4;

    /// <summary>After a practice session the tamer's taming rests this long: it hunts, banks and sells.</summary>
    public static readonly TimeSpan PracticeRest = TimeSpan.FromHours(2);

    /// <summary>
    /// A tamer with a fighting pet tames a new one only this many times as strong: a grizzly
    /// gives way to a drake, a drake to a dragon, never a drake to another drake.
    /// </summary>
    public const double UpgradeFactor = 1.5;

    /// <summary>A beast this share of the strongest one as strong is as good a pick; the nearer ground wins then.</summary>
    public const double ShareOfBest = 0.8;

    public const int NoChoice = -1;

    private const double NoOdds = 0;
    private const double FullOdds = 1;

    /// <summary>The engine's gate: its cursor gives no chance at all below the beast's minimum.</summary>
    public static bool CanAttempt(double tamerSkill, double minTameSkill) => tamerSkill >= minTameSkill;

    /// <summary>The engine's odds of one taming try, from zero to one.</summary>
    public static double TameOdds(double tamerSkill, double minTameSkill, int owners)
    {
        var biased = minTameSkill + Math.Max(0, owners) * EngineOwnerPenalty + EngineMinSkillBias;
        var low = biased - EngineCheckWindow;
        var odds = (tamerSkill - low) / (EngineCheckWindow * 2);
        return Math.Clamp(odds, NoOdds, FullOdds);
    }

    /// <summary>How hard a beast fights, on the scale the danger check rates foes by.</summary>
    public static int BeastPower(int hitsMax, int strength, int damageMin, int damageMax) =>
        ThreatRating.Score([new HostileStats(Math.Max(0, hitsMax), Math.Max(0, strength), Math.Max(0, (damageMin + damageMax) / 2))]);

    /// <summary>The least power of a pet a tamer at <paramref name="taming"/> keeps and fights beside.</summary>
    public static int KeepPower(double taming) =>
        taming >= SkillTierRules.MasterSkill ? MasterKeepPower
        : taming >= SkillTierRules.AdeptSkill ? AdeptKeepPower
        : taming >= SkillTierRules.ExpertSkill ? ExpertKeepPower
        : NoviceKeepPower;

    /// <summary>A beast strong enough for a tamer at <paramref name="taming"/> to keep.</summary>
    public static bool IsKeeper(int power, double taming) => power >= KeepPower(taming);

    /// <summary>A beast far stronger than the tamer's fighting pet, or any beast while it owns none.</summary>
    public static bool IsUpgrade(int power, int fighterPower) => fighterPower <= 0 || power >= fighterPower * UpgradeFactor;

    /// <summary>A keeper for the tamer's tier that its fighting pet does not already match.</summary>
    public static bool IsWantedKeeper(int power, TamerFacts tamer) =>
        IsKeeper(power, tamer.Taming) && IsUpgrade(power, tamer.FighterPower);

    /// <summary>A town or farm beast: never a pet, never practice.</summary>
    public static bool IsLowBeast(int power) => power < LowBeastPower;

    /// <summary>The least power of a beast a tamer at <paramref name="taming"/> practises on.</summary>
    public static int PracticePower(double taming) =>
        taming >= SkillTierRules.ExpertSkill ? ExpertPracticePower : LowBeastPower;

    /// <summary>
    /// The engine's gain rules for a tamer's Animal Taming: the skill is below its cap and set
    /// to rise, and the skill total has room or another skill set to fall can give a point.
    /// A grandmaster, or a tamer whose 700 points are spent, practises no more.
    /// </summary>
    public static bool StillGains(double skillBase, double skillCap, bool lockUp, int totalFixed, int totalCapFixed, bool otherFalls) =>
        skillBase < skillCap && lockUp && (totalFixed < totalCapFixed || otherFalls);

    /// <summary>
    /// A beast worth a practice tame: the tamer practises now, the beast is hard enough to
    /// teach and strong enough for the tamer's tier.
    /// </summary>
    public static bool IsPracticeBeast(int power, double odds, TamerFacts tamer) =>
        tamer.Practises && odds <= MaxPracticeOdds && power >= PracticePower(tamer.Taming);

    /// <summary>
    /// True when a beast of this kind is worth a tamer's try: the engine lets it try, the
    /// follower slots hold it, the odds and the control are good enough, it is no town or
    /// farm beast, and it is a keeper the tamer wants (<see cref="IsWantedKeeper"/>) or a
    /// practice beast it may take now.
    /// <paramref name="owners"/> counts the beast's earlier owners.
    /// </summary>
    public static bool IsQuarry(BeastProfile beast, TamerFacts tamer, int owners, double controlChance)
    {
        var odds = TameOdds(tamer.Taming, beast.MinTameSkill, owners);

        return (tamer.Female ? beast.AllowFemaleTamer : beast.AllowMaleTamer) &&
               !beast.SubdueBeforeTame &&
               owners < EngineMaxOwners &&
               CanAttempt(tamer.Taming, beast.MinTameSkill) &&
               tamer.Followers + beast.ControlSlots <= tamer.FollowersMax &&
               odds >= MinTameOdds &&
               controlChance >= MinControlChance &&
               !IsLowBeast(beast.Power) &&
               (IsWantedKeeper(beast.Power, tamer) || IsPracticeBeast(beast.Power, odds, tamer));
    }

    /// <summary>
    /// A wild beast the tamer may try now: <see cref="IsQuarry"/> on a live one that is
    /// tamable, has no master, and never had this tamer as owner (the engine gives that one
    /// back with no skill check at all).
    /// </summary>
    public static bool IsLiveQuarry(
        bool tamable,
        bool controlled,
        bool ownedBefore,
        BeastProfile beast,
        TamerFacts tamer,
        int owners,
        double controlChance
    ) =>
        tamable && !controlled && !ownedBefore && IsQuarry(beast, tamer, owners, controlChance);

    /// <summary>
    /// The pick among beasts or grounds: the strongest, or a nearer one at least
    /// <see cref="ShareOfBest"/> as strong. <see cref="NoChoice"/> when there is none.
    /// </summary>
    public static int Choose(IReadOnlyList<(int Power, int Distance)> options)
    {
        var best = 0;

        for (var i = 0; i < (options?.Count ?? 0); i++)
        {
            best = Math.Max(best, options[i].Power);
        }

        var pick = NoChoice;
        var bar = best * ShareOfBest;

        for (var i = 0; i < (options?.Count ?? 0); i++)
        {
            if (options[i].Power >= bar && (pick == NoChoice || options[i].Distance < options[pick].Distance))
            {
                pick = i;
            }
        }

        return pick;
    }

    /// <summary>
    /// Which of its pets on foot a tamer keeps: its one fighter, the strongest pet strong for
    /// its tier, and while it rides none, the strongest mount among the rest. Every other pet
    /// is spare and goes back to the wild: a bull at heel is no mage-tamer's pet, and a second
    /// drake is one the tamer leaves behind.
    /// </summary>
    public static bool[] Keeps(IReadOnlyList<OwnedPet> pets, bool ownerRides, double taming)
    {
        var count = pets?.Count ?? 0;
        var kept = new bool[count];
        var fighter = Strongest(pets, index => IsKeeper(pets[index].Power, taming));

        if (fighter != NoChoice)
        {
            kept[fighter] = true;
        }

        if (!ownerRides)
        {
            var mount = Strongest(pets, index => index != fighter && pets[index].IsMount);

            if (mount != NoChoice)
            {
                kept[mount] = true;
            }
        }

        return kept;
    }

    /// <summary>The power of the fighter a tamer keeps among these pets (<see cref="Keeps"/>), zero with none.</summary>
    public static int FighterPower(IReadOnlyList<OwnedPet> pets, double taming)
    {
        var fighter = Strongest(pets, index => IsKeeper(pets[index].Power, taming));
        return fighter == NoChoice ? 0 : pets[fighter].Power;
    }

    private static int Strongest(IReadOnlyList<OwnedPet> pets, Func<int, bool> eligible)
    {
        var pick = NoChoice;

        for (var i = 0; i < (pets?.Count ?? 0); i++)
        {
            if (eligible(i) && (pick == NoChoice || pets[i].Power > pets[pick].Power))
            {
                pick = i;
            }
        }

        return pick;
    }

    /// <summary>A practice session ends at its cap of let-go beasts.</summary>
    public static bool SessionFull(int practiced) => practiced >= PracticeTamesPerSession;
}
