using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A station trade is ready only with the makings of a piece the crafter works at even odds, and
/// a smith back from the mine smelts its ore before it works the forge. A Jhelom tailor with a
/// few scraps of cloth walked to its station and stood out of cloth for three minutes, four
/// times in an hour.
/// </summary>
public class CraftMakingsTests
{
    // A serial range no other test class uses: tests that share serials collide in the one engine world.
    private const uint FirstSerial = 0x6D01;
    private const uint BackpackSerialOffset = 0x80;
    private const double Grandmaster = 100.0;
    private const double Untrained = 0.0;
    private const int IngotStack = 100;
    private const int Scrap = 1;
    private const int OreStack = 20;

    private static uint _nextSerial = FirstSerial;

    static CraftMakingsTests()
    {
        Timer.Init(0);
        TestSkills.EnsureTable();

        if (DefBlacksmithy.CraftSystem == null)
        {
            DefBlacksmithy.Initialize();
        }
    }

    public CraftMakingsTests() => TestMap.EnsureInternal();

    [Fact]
    public void CarriesMakings_ASmithWithIngotsForAPiece()
    {
        var smith = Crafter(Grandmaster);
        InPack(smith, new IronIngot(NextSerial()) { Amount = IngotStack });

        Assert.True(CraftStations.CarriesMakings(smith, SmithRules.Trade));
    }

    [Fact]
    public void CarriesMakings_AScrapOfStockIsNoSession()
    {
        var smith = Crafter(Grandmaster);
        InPack(smith, new IronIngot(NextSerial()) { Amount = Scrap });

        Assert.True(CraftStations.CarriesStock(smith, SmithRules.Trade));
        Assert.False(CraftStations.CarriesMakings(smith, SmithRules.Trade));
    }

    [Fact]
    public void WorksAtUsefulOdds_BelowEvenOddsIsNoWork()
    {
        var dagger = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(Dagger));

        Assert.True(CraftStationSkill.WorksAtUsefulOdds(Crafter(Grandmaster), DefBlacksmithy.CraftSystem, dagger));
        Assert.False(CraftStationSkill.WorksAtUsefulOdds(Crafter(Untrained), DefBlacksmithy.CraftSystem, dagger));
    }

    [Fact]
    public void CarriesMakings_NoneWithoutStock()
    {
        var smith = Crafter(Grandmaster);

        Assert.False(CraftStations.CarriesMakings(smith, SmithRules.Trade));
        Assert.False(CraftStations.CarriesMakings(null, SmithRules.Trade));
        Assert.False(CraftStations.CarriesMakings(smith, null));
    }

    [Fact]
    public void SmeltsFirst_ASmithBackFromTheMineWithOre()
    {
        var smith = Crafter(Grandmaster);

        Assert.False(CraftStations.SmeltsFirst(smith, SmithRules.Trade));

        InPack(smith, new IronOre(NextSerial()) { Amount = OreStack });

        Assert.True(CraftStations.SmeltsFirst(smith, SmithRules.Trade));
        Assert.False(CraftStations.SmeltsFirst(smith, CarpentryRules.Trade));
    }

    private static Serial NextSerial() => (Serial)_nextSerial++;

    private static SosariaCharacter Crafter(double smithing)
    {
        var serial = NextSerial();
        var crafter = new SosariaCharacter(serial);
        crafter.DefaultMobileInit();
        crafter.Skills[SkillName.Blacksmith].Base = smithing;

        var pack = new Backpack((Serial)(serial.Value + BackpackSerialOffset)) { Layer = Layer.Backpack, Movable = true };
        pack.Parent = crafter;
        crafter.Items.Add(pack);
        return crafter;
    }

    private static void InPack(Mobile owner, Item item) => owner.Backpack.AddItem(item);
}
