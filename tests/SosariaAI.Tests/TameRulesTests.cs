using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TameRulesTests
{
    // Engine numbers (UOContent Mobiles): minimum taming, control slots, and the power of the
    // full hits (pre-AOS: the set hits times 100 / 60), strength and average blow.
    private const double DragonSkill = 93.9;
    private const double NightmareSkill = 95.1;
    private const double DrakeSkill = 84.3;
    private const double WhiteWyrmSkill = 96.3;
    private const double GrizzlySkill = 59.1;
    private const double PolarBearSkill = 35.1;
    private const double BullSkill = 71.1;
    private const double GiantSpiderSkill = 59.1;
    private const double SnowLeopardSkill = 53.1;
    private const double TimberWolfSkill = 23.1;
    private const double CowSkill = 11.1;
    private const double HorseSkill = 29.1;

    private const int DragonPower = 870;
    private const int NightmarePower = 554;
    private const int WhiteWyrmPower = 798;
    private const int DrakePower = 449;
    private const int GrizzlyPower = 157;
    private const int PolarBearPower = 143;
    private const int BullPower = 104;
    private const int BlackBearPower = 99;
    private const int GiantSpiderPower = 101;
    private const int SnowLeopardPower = 77;
    private const int TimberWolfPower = 78;
    private const int EaglePower = 49;
    private const int CowPower = 34;
    private const int HorsePower = 67;

    private const int DragonSlots = 3;
    private const int NightmareSlots = 2;
    private const int OneSlot = 1;
    private const int Stable = 5;
    private const int NoFollowers = 0;
    private const int NoOwners = 0;
    private const int NoFighter = 0;
    private const double FullControl = 0.99;
    private const double PoorControl = 0.5;

    private const double Grandmaster = 100;
    private const double Master = 95;
    private const double Adept = 85;
    private const double Expert = 75;
    private const double Journeyman = 65;

    private const int SkillTotalCapFixed = 7000;
    private const int RoomInTotal = 6500;

    private static BeastProfile Beast(string kind, double skill, int slots, int power, bool female = true, bool male = true) =>
        new(kind, skill, slots, power, female, male, SubdueBeforeTame: false);

    private static TamerFacts Tamer(
        double taming,
        int followers = NoFollowers,
        bool female = false,
        bool practises = true,
        int fighterPower = NoFighter
    ) =>
        new(taming, female, followers, Stable, practises, fighterPower);

    [Fact]
    public void CanAttempt_IsTheEngineGate()
    {
        // The engine's cursor says "You have no chance" below the minimum, and tries at it.
        Assert.True(TameRules.CanAttempt(DragonSkill, DragonSkill));
        Assert.False(TameRules.CanAttempt(DragonSkill - 0.1, DragonSkill));
    }

    [Fact]
    public void TameOdds_FollowTheEngineCheck()
    {
        // (skill - (min + 24.9 - 25)) / 50: a grandmaster has one in eight on a dragon, one in ten on a nightmare.
        Assert.Equal(0.124, TameRules.TameOdds(Grandmaster, DragonSkill, NoOwners), 3);
        Assert.Equal(0.1, TameRules.TameOdds(Grandmaster, NightmareSkill, NoOwners), 3);
        Assert.Equal(0.0, TameRules.TameOdds(DragonSkill - 1, DragonSkill, NoOwners));
        Assert.Equal(1.0, TameRules.TameOdds(Grandmaster, CowSkill, NoOwners));
    }

    [Fact]
    public void TameOdds_EachEarlierOwnerMakesItHarder()
    {
        Assert.True(TameRules.TameOdds(Grandmaster, DrakeSkill, 1) < TameRules.TameOdds(Grandmaster, DrakeSkill, NoOwners));
        Assert.Equal(
            TameRules.TameOdds(Grandmaster, DrakeSkill + TameRules.EngineOwnerPenalty, NoOwners),
            TameRules.TameOdds(Grandmaster, DrakeSkill, 1),
            6);
    }

    [Fact]
    public void BeastPower_IsTheThreatScoreOfTheFullBeast()
    {
        // A dragon: 811 hits, 810 strength, 16 to 22 a blow.
        Assert.Equal(811 + 810 / 20 + 19, TameRules.BeastPower(811, 810, 16, 22));
        Assert.True(TameRules.IsKeeper(TameRules.BeastPower(811, 810, 16, 22), Grandmaster));
    }

    [Fact]
    public void FarmAndTownBeasts_AreLow_WildFightersAreNot()
    {
        Assert.True(TameRules.IsLowBeast(CowPower));
        Assert.False(TameRules.IsLowBeast(EaglePower));
        Assert.False(TameRules.IsLowBeast(TimberWolfPower));
    }

    [Fact]
    public void KeepPower_GrowsWithTheTamersTier()
    {
        Assert.Equal(TameRules.NoviceKeepPower, TameRules.KeepPower(Journeyman));
        Assert.Equal(TameRules.ExpertKeepPower, TameRules.KeepPower(Expert));
        Assert.Equal(TameRules.AdeptKeepPower, TameRules.KeepPower(Adept));
        Assert.Equal(TameRules.MasterKeepPower, TameRules.KeepPower(Master));
        Assert.Equal(TameRules.MasterKeepPower, TameRules.KeepPower(Grandmaster));
    }

    [Fact]
    public void IsKeeper_NoTamerKeepsABullABlackBearOrAGiantSpider()
    {
        // One night kept 30 bulls, 20 black bears and 15 giant spiders.
        foreach (var taming in new[] { Journeyman, Expert, Adept, Master, Grandmaster })
        {
            Assert.False(TameRules.IsKeeper(BullPower, taming));
            Assert.False(TameRules.IsKeeper(BlackBearPower, taming));
            Assert.False(TameRules.IsKeeper(GiantSpiderPower, taming));
        }
    }

    [Fact]
    public void IsKeeper_BearsServeTheMiddleTiers_DrakesAndDragonsTheMasters()
    {
        Assert.True(TameRules.IsKeeper(PolarBearPower, Journeyman));
        Assert.True(TameRules.IsKeeper(GrizzlyPower, Expert));
        Assert.True(TameRules.IsKeeper(GrizzlyPower, Adept));
        Assert.False(TameRules.IsKeeper(GrizzlyPower, Master));
        Assert.True(TameRules.IsKeeper(DrakePower, Master));
        Assert.True(TameRules.IsKeeper(NightmarePower, Grandmaster));
    }

    [Fact]
    public void IsQuarry_NeverACow()
    {
        // Tamers took the sheep and cats of the Britain field and the bank.
        Assert.False(TameRules.IsQuarry(Beast("Cow", CowSkill, OneSlot, CowPower), Tamer(Journeyman), NoOwners, FullControl));
    }

    [Fact]
    public void IsQuarry_AGrandmasterTakesDragonsNightmaresAndWyrms()
    {
        var tamer = Tamer(Grandmaster, practises: false);

        Assert.True(TameRules.IsQuarry(Beast("Dragon", DragonSkill, DragonSlots, DragonPower), tamer, NoOwners, FullControl));
        Assert.True(TameRules.IsQuarry(Beast("Nightmare", NightmareSkill, NightmareSlots, NightmarePower), tamer, NoOwners, FullControl));
        Assert.True(TameRules.IsQuarry(Beast("WhiteWyrm", WhiteWyrmSkill, DragonSlots, WhiteWyrmPower), tamer, NoOwners, FullControl));
    }

    [Fact]
    public void IsQuarry_AMasterLeavesTheDragon_ForTheDrake()
    {
        var tamer = Tamer(Master);

        Assert.False(TameRules.IsQuarry(Beast("Dragon", DragonSkill, DragonSlots, DragonPower), tamer, NoOwners, FullControl));
        Assert.True(TameRules.IsQuarry(Beast("Drake", DrakeSkill, NightmareSlots, DrakePower), tamer, NoOwners, FullControl));
    }

    [Fact]
    public void IsQuarry_RespectsSlotsSexOwnersAndControl()
    {
        var dragon = Beast("Dragon", DragonSkill, DragonSlots, DragonPower);
        var unicorn = Beast("Unicorn", NightmareSkill, NightmareSlots, NightmarePower, female: true, male: false);

        Assert.False(TameRules.IsQuarry(dragon, Tamer(Grandmaster, followers: 3), NoOwners, FullControl));
        Assert.False(TameRules.IsQuarry(unicorn, Tamer(Grandmaster, female: false), NoOwners, FullControl));
        Assert.True(TameRules.IsQuarry(unicorn, Tamer(Grandmaster, female: true), NoOwners, FullControl));
        Assert.False(TameRules.IsQuarry(dragon, Tamer(Grandmaster), TameRules.EngineMaxOwners, FullControl));
        Assert.False(TameRules.IsQuarry(dragon, Tamer(Grandmaster), NoOwners, PoorControl));
        Assert.False(TameRules.IsQuarry(dragon with { SubdueBeforeTame = true }, Tamer(Grandmaster), NoOwners, FullControl));
    }

    [Fact]
    public void IsLiveQuarry_SkipsTameBeastsAndOnesItOwnedBefore()
    {
        var drake = Beast("Drake", DrakeSkill, NightmareSlots, DrakePower);
        var tamer = Tamer(Master);

        Assert.True(TameRules.IsLiveQuarry(true, false, false, drake, tamer, NoOwners, FullControl));
        Assert.False(TameRules.IsLiveQuarry(false, false, false, drake, tamer, NoOwners, FullControl));
        Assert.False(TameRules.IsLiveQuarry(true, true, false, drake, tamer, NoOwners, FullControl));
        Assert.False(TameRules.IsLiveQuarry(true, false, true, drake, tamer, 1, FullControl));
    }

    [Fact]
    public void Choose_TakesTheStrongest_OrANearerOneAboutAsStrong()
    {
        Assert.Equal(0, TameRules.Choose([(DragonPower, 90), (DrakePower, 10)]));
        Assert.Equal(1, TameRules.Choose([(DragonPower, 90), (DragonPower - 10, 10)]));
        Assert.Equal(TameRules.NoChoice, TameRules.Choose([]));
    }

    [Fact]
    public void Keeps_OneFighterAndOneMount_EveryOtherPetGoes()
    {
        // Two drakes, a horse, a second horse and a bull: the stronger drake and one horse stay.
        var kept = TameRules.Keeps(
            [
                new OwnedPet(DrakePower, IsMount: false),
                new OwnedPet(DrakePower + 20, IsMount: false),
                new OwnedPet(HorsePower, IsMount: true),
                new OwnedPet(HorsePower - 5, IsMount: true),
                new OwnedPet(BullPower, IsMount: false)
            ],
            ownerRides: false,
            Master
        );

        Assert.Equal([false, true, true, false, false], kept);
    }

    [Fact]
    public void Keeps_NoMountOnFootWhileTheTamerRides()
    {
        var kept = TameRules.Keeps([new OwnedPet(DragonPower, IsMount: false), new OwnedPet(HorsePower, IsMount: true)], ownerRides: true, Master);

        Assert.Equal([true, false], kept);
    }

    [Fact]
    public void Keeps_ABeastTooWeakForTheTierIsNoFighter()
    {
        // A grizzly serves an Adept, never a Master.
        Assert.Equal([true], TameRules.Keeps([new OwnedPet(GrizzlyPower, IsMount: false)], ownerRides: true, Adept));
        Assert.Equal([false], TameRules.Keeps([new OwnedPet(GrizzlyPower, IsMount: false)], ownerRides: true, Master));
    }

    [Fact]
    public void Keeps_AMountingFighterLeavesTheMountSlotToAnother()
    {
        var kept = TameRules.Keeps(
            [new OwnedPet(NightmarePower, IsMount: true), new OwnedPet(HorsePower, IsMount: true)],
            ownerRides: false,
            Grandmaster
        );

        Assert.Equal([true, true], kept);
    }

    [Fact]
    public void FighterPower_IsTheStrongestKeeper_ZeroWithNone()
    {
        Assert.Equal(DrakePower, TameRules.FighterPower([new OwnedPet(HorsePower, IsMount: true), new OwnedPet(DrakePower, IsMount: false)], Master));
        Assert.Equal(NoFighter, TameRules.FighterPower([new OwnedPet(BullPower, IsMount: false)], Master));
        Assert.Equal(NoFighter, TameRules.FighterPower([], Master));
    }

    [Fact]
    public void IsUpgrade_OnlyAFarStrongerBeast()
    {
        Assert.True(TameRules.IsUpgrade(DrakePower, NoFighter));
        Assert.True(TameRules.IsUpgrade(DrakePower, GrizzlyPower));
        Assert.True(TameRules.IsUpgrade(DragonPower, DrakePower));
        Assert.False(TameRules.IsUpgrade(DrakePower + 20, DrakePower));
        Assert.False(TameRules.IsUpgrade(PolarBearPower, GrizzlyPower));
    }

    [Fact]
    public void IsQuarry_ATamerWithADrake_TakesNoSecondDrake_ButADragon()
    {
        var tamer = Tamer(Grandmaster, followers: NightmareSlots, practises: false, fighterPower: DrakePower);

        Assert.False(TameRules.IsQuarry(Beast("Drake", DrakeSkill, NightmareSlots, DrakePower), tamer, NoOwners, FullControl));
        Assert.True(TameRules.IsQuarry(Beast("Dragon", DragonSkill, DragonSlots, DragonPower), tamer, NoOwners, FullControl));
    }

    [Fact]
    public void IsQuarry_ATamerWithAFighterStillPractises()
    {
        var tamer = Tamer(Expert, followers: OneSlot, fighterPower: GrizzlyPower);

        Assert.True(TameRules.IsQuarry(Beast("Bull", BullSkill, OneSlot, BullPower), tamer, NoOwners, FullControl));
        Assert.False(TameRules.IsQuarry(Beast("GrizzlyBear", GrizzlySkill, OneSlot, GrizzlyPower), Tamer(Expert, practises: false, fighterPower: GrizzlyPower), NoOwners, FullControl));
    }

    [Fact]
    public void IsQuarry_AnExpertPractisesOnBulls_NeverOnLeopards()
    {
        var tamer = Tamer(Expert);

        Assert.True(TameRules.IsQuarry(Beast("Bull", BullSkill, OneSlot, BullPower), tamer, NoOwners, FullControl));
        Assert.False(TameRules.IsQuarry(Beast("SnowLeopard", SnowLeopardSkill, OneSlot, SnowLeopardPower), tamer, NoOwners, FullControl));
        Assert.True(TameRules.IsQuarry(Beast("GiantSpider", GiantSpiderSkill, OneSlot, GiantSpiderPower), tamer, NoOwners, FullControl));
        Assert.True(TameRules.IsQuarry(Beast("GrizzlyBear", GrizzlySkill, OneSlot, GrizzlyPower), tamer, NoOwners, FullControl));
    }

    [Fact]
    public void IsQuarry_AJourneymanPractisesOnLeopards_NotOnSureTames()
    {
        var tamer = Tamer(Journeyman);

        Assert.True(TameRules.IsQuarry(Beast("SnowLeopard", SnowLeopardSkill, OneSlot, SnowLeopardPower), tamer, NoOwners, FullControl));
        Assert.False(TameRules.IsQuarry(Beast("TimberWolf", TimberWolfSkill, OneSlot, TimberWolfPower), tamer, NoOwners, FullControl));
        Assert.False(TameRules.IsQuarry(Beast("Horse", HorseSkill, OneSlot, HorsePower), tamer, NoOwners, FullControl));
        Assert.True(TameRules.IsQuarry(Beast("PolarBear", PolarBearSkill, OneSlot, PolarBearPower), tamer, NoOwners, FullControl));
    }

    [Fact]
    public void IsQuarry_ATamerOutOfPractice_GoesForKeepersOnly()
    {
        var tamer = Tamer(Expert, practises: false);

        Assert.False(TameRules.IsQuarry(Beast("Bull", BullSkill, OneSlot, BullPower), tamer, NoOwners, FullControl));
        Assert.True(TameRules.IsQuarry(Beast("GrizzlyBear", GrizzlySkill, OneSlot, GrizzlyPower), tamer, NoOwners, FullControl));
    }

    [Fact]
    public void StillGains_FollowsTheEngineGainRules()
    {
        Assert.True(TameRules.StillGains(Expert, Grandmaster, lockUp: true, RoomInTotal, SkillTotalCapFixed, otherFalls: false));
        Assert.False(TameRules.StillGains(Grandmaster, Grandmaster, lockUp: true, RoomInTotal, SkillTotalCapFixed, otherFalls: false));
        Assert.False(TameRules.StillGains(Expert, Grandmaster, lockUp: false, RoomInTotal, SkillTotalCapFixed, otherFalls: false));
        Assert.False(TameRules.StillGains(Expert, Grandmaster, lockUp: true, SkillTotalCapFixed, SkillTotalCapFixed, otherFalls: false));
        Assert.True(TameRules.StillGains(Expert, Grandmaster, lockUp: true, SkillTotalCapFixed, SkillTotalCapFixed, otherFalls: true));
    }

    [Fact]
    public void SessionFull_AtTheSessionCap()
    {
        Assert.False(TameRules.SessionFull(TameRules.PracticeTamesPerSession - 1));
        Assert.True(TameRules.SessionFull(TameRules.PracticeTamesPerSession));
    }
}
