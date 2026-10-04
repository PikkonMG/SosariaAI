using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Economy;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class RuneKitTests
{
    private static readonly Point3D ShameCamp = new(517, 1559, 0);
    private static readonly Point3D YewGateCamp = new(787, 736, 0);
    private static readonly Point3D BritainGateCamp = new(1336, 2013, 0);

    [Fact]
    public void RedLandings_OnlyWhatNoRoadReaches_AndNeverUnderTheGuards()
    {
        // The Den's gate walks a red to the Yew gate; Britain's pad has guards; Shame's door
        // lies past Yew's guards, so only a rune reaches it.
        var picks = RuneKit.RedLandings(
            [YewGateCamp, BritainGateCamp, ShameCamp],
            landing => landing == BritainGateCamp,
            landing => landing == YewGateCamp
        );

        Assert.Equal([ShameCamp], picks);
    }

    private const double DexxerMagery = 35;
    private const double NoviceDexxerMagery = 22;
    private const double WorkerMagery = 0;

    private static readonly Point3D BritainBank = new(1434, 1699, 0);
    private static readonly Point3D BritainCorner = new(1460, 1720, 0);
    private static readonly Point3D DestardDoor = new(1176, 2635, 0);
    private static readonly Point3D YewWoods = new(600, 1000, 0);
    private static readonly Point3D MinocMine = new(2560, 500, 0);

    [Theory]
    [InlineData(DexxerMagery, 0, true)]
    [InlineData(NoviceDexxerMagery, 0, false)]
    [InlineData(RecallRules.ScrollMinMagery, 2, true)]
    [InlineData(WorkerMagery, 5, false)]
    public void TravelsByMagic_ABookOrScrollsWithSkillToReadThem(double magery, int scrolls, bool travels) =>
        Assert.Equal(travels, RuneKit.TravelsByMagic(magery, scrolls));

    [Theory]
    [InlineData(SkillTier.Novice, true, false)]
    [InlineData(SkillTier.Apprentice, true, false)]
    [InlineData(SkillTier.Journeyman, true, true)]
    [InlineData(SkillTier.Grandmaster, false, false)]
    public void CarriesMarkedRunes_EstablishedTravelersOnly(SkillTier tier, bool travels, bool carries) =>
        Assert.Equal(carries, RuneKit.CarriesMarkedRunes(tier, travels));

    [Fact]
    public void Places_OneRunePerPlaceAndNoUnknownPlace()
    {
        var places = RuneKit.Places([BritainBank, BritainCorner, Point3D.Zero, DestardDoor]);

        Assert.Equal([BritainBank, DestardDoor], places);
    }

    [Fact]
    public void Places_NoMoreThanTheRuneTarget()
    {
        var places = RuneKit.Places([BritainBank, DestardDoor, YewWoods, MinocMine, new Point3D(4400, 1150, 0)]);

        Assert.Equal(SupplyRules.RuneTarget, places.Count);
        Assert.Empty(RuneKit.Places(null));
    }

    [Fact]
    public void DoorsToMark_NearestFirst_SkipsDoorsAlreadyMarkedAndStopsWhenTheBookIsFull()
    {
        var shameDoor = new Point3D(514, 1561, 0);
        var covetousDoor = new Point3D(2499, 919, 0);
        Point3D[] doors = [DestardDoor, Point3D.Zero, shameDoor, covetousDoor];
        Point3D[] marked = [new(DestardDoor.X + 3, DestardDoor.Y, 0)];

        Assert.Equal([shameDoor, covetousDoor], RuneKit.DoorsToMark(doors, marked, room: RunebookRules.EntryCap));
        Assert.Equal([shameDoor], RuneKit.DoorsToMark(doors, marked, room: 1));
        Assert.Empty(RuneKit.DoorsToMark(doors, marked, room: 0));
        Assert.Empty(RuneKit.DoorsToMark(null, marked, room: 1));
    }

    [Theory]
    [InlineData(true, RunebookRules.SpareBlankRunes)]
    [InlineData(false, 0)]
    public void SpareBlanks_AMarkingMageKeepsOneBlankBesideItsBook(bool marks, int spares) =>
        Assert.Equal(spares, RuneKit.SpareBlanks(marks));

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void PacksKit_OnceForACharacterAlreadyInTheWorld(bool firstDay, bool packedBefore, bool carriesMarked, bool packs) =>
        Assert.Equal(packs, RuneKit.PacksKit(firstDay, packedBefore, carriesMarked));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Pack_NoCharacter_DoesNothing(bool firstDay) => RuneKit.Pack(null, firstDay);

    [Fact]
    public void TownBanks_EveryTownBankNearestHomeFirst()
    {
        var minoc = Place("Bank of Minoc", TownTripRules.BankKind, 2503, 552);
        var trinsic = Place("Trinsic Royal Bank", TownTripRules.BankKind, 1897, 2684);
        var britain = Place("The First Bank of Britain", TownTripRules.BankKind, 1425, 1690);
        var smith = Place("Minoc Smith", "Vendor", 2520, 560);

        var banks = RuneKit.TownBanks([minoc, smith, trinsic, britain], BritainBank);

        Assert.Equal([britain, trinsic, minoc], banks);
    }

    [Fact]
    public void TownBanks_LeavesOutTheRedsTownAndUnplacedBanks()
    {
        // A blue's trip to a Den shop took the moongate there, and the anti-PK fought reds for an hour.
        var den = Place("A Place Fer Yer Stuff", TownTripRules.BankKind, 2731, 2192);
        var unplaced = Place("Nowhere Bank", TownTripRules.BankKind, 0, 0);
        var yew = Place("Abbey Banker", TownTripRules.BankKind, 652, 820);

        Assert.Equal([yew], RuneKit.TownBanks([den, unplaced, null, yew], BritainBank));
        Assert.Empty(RuneKit.TownBanks(null, BritainBank));
    }

    [Fact]
    public void TownBanks_OneTellerPerTown()
    {
        // The catalog lists each town's bank once by name and once per banker at the counter.
        var named = Place("Bank of Minoc", TownTripRules.BankKind, 2503, 552);
        var banker = Place("Banker 2503-552", TownTripRules.BankKind, 2503, 552);
        var skara = Place("Bank of Skara Brae", TownTripRules.BankKind, 587, 2146);

        Assert.Equal([skara, named], RuneKit.TownBanks([named, banker, skara], new Point3D(600, 2100, 0)));
    }

    [Fact]
    public void UsualFirst_TheDoorsOfDungeonsThatFitComeFirst_EachPartInItsOrder()
    {
        // Nearest first a Britain fighter's book filled with the Sewer and the Orc Cave it
        // never delved before Despise and Covetous came up.
        DungeonDoor sewer = new("Britain Sewer", new Point3D(1490, 1640, 24));
        DungeonDoor orcCave = new("Orc Cave", new Point3D(1014, 1434, 0));
        DungeonDoor despise = new("Despise", new Point3D(1298, 1080, 0));
        DungeonDoor covetous = new("Covetous", new Point3D(2499, 919, 0));
        var usual = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "despise", "Covetous" };

        var ordered = RuneKit.UsualFirst([sewer, orcCave, despise, covetous], usual);

        Assert.Equal([despise, covetous, sewer, orcCave], ordered);
        Assert.Equal([sewer, orcCave], RuneKit.UsualFirst([sewer, orcCave], null));
    }

    private static Destination Place(string name, string kind, int x, int y) =>
        new() { Name = name, Kind = kind, X = x, Y = y, Z = 0 };
    [Theory]
    [InlineData(false, RunebookRules.EntryCap, true)]
    [InlineData(true, RunebookRules.EntryCap, false)]
    [InlineData(false, RunebookRules.EntryCap - 1, false)]
    public void MakesRoomForDen_OnlyAFullBookWithoutADenEntry(bool denMarked, int entries, bool makesRoom) =>
        Assert.Equal(makesRoom, RuneKit.MakesRoomForDen(denMarked, entries));
}
