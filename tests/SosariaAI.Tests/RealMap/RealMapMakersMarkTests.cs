using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// A grandmaster's craft through the real engine, and what the engine's vendors pay for the
/// goods. The anvil and forge are placed on the Britain smithy floor; the vendors are the
/// engine's own, with their era sell tables. Runs in the real-map collection with the world
/// <see cref="KitWorld"/> sets up, and skips without the client data.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapMakersMarkTests
{
    private const uint SmithSerial = 0x7F7001;
    private const uint PackSerial = 0x7F7002;
    private const uint FirstItemSerial = 0x40F7F001;
    private const double Grandmaster = 100;
    private const int IngotLoad = 10;
    private const int DaggerIngots = 3;
    private const int OneTile = 1;

    private static readonly Point3D SmithyFloor = new(1423, 1557, 30);

    private readonly List<IEntity> _made = [];
    private uint _nextItem = FirstItemSerial;

    public RealMapMakersMarkTests()
    {
        if (RealMapWorld.Available)
        {
            KitWorld.Ensure();
        }
    }

    [RealMapFact]
    public void MakersMarkAnswer_TheGrandmastersExceptionalDaggerComesIntoThePack_AndBurnsItsIngots()
    {
        // A grandmaster smith lost 1027 daggers in one evening: the T2A menus asked it whether to
        // mark each exceptional piece, and it never answered. The answer makes the piece.
        var map = RealMapWorld.Felucca;
        var smith = new SosariaCharacter((Serial)SmithSerial) { Name = "Grandmaster Smith" };
        _made.Add(smith);
        smith.DefaultMobileInit();
        var pack = new Backpack((Serial)PackSerial) { Layer = Layer.Backpack, Movable = true };
        pack.Parent = smith;
        smith.Items.Add(pack);
        smith.MoveToWorld(SmithyFloor, map);
        smith.Skills[SkillName.Blacksmith].Base = Grandmaster;
        Place(new Anvil((Serial)_nextItem++), new Point3D(SmithyFloor.X + OneTile, SmithyFloor.Y, SmithyFloor.Z));
        Place(new Forge((Serial)_nextItem++), new Point3D(SmithyFloor.X - OneTile, SmithyFloor.Y, SmithyFloor.Z));
        var hammer = InPack(pack, new SmithHammer((Serial)_nextItem++));
        InPack(pack, new IronIngot((Serial)_nextItem++) { Amount = IngotLoad });
        var system = DefBlacksmithy.CraftSystem;
        var dagger = system.CraftItems.SearchFor(typeof(Dagger));

        try
        {
            Assert.True(
                CraftTradeRules.AnswersMakersMark(
                    menusAsk: true,
                    smith.Skills[SkillName.Blacksmith].Base,
                    dagger.IsMarkable(typeof(Dagger)),
                    madeNothing: true,
                    materialsKept: true
                )
            );

            dagger.CompleteCraft(CraftTradeRules.ExceptionalQuality, makersMark: true, smith, system, typeof(IronIngot), hammer, null);

            var made = pack.FindItemByType<Dagger>();

            Assert.NotNull(made);
            _made.Add(made);
            Assert.Equal(WeaponQuality.Exceptional, made.Quality);
            Assert.Equal(smith.RawName, made.Crafter);
            Assert.Equal(IngotLoad - DaggerIngots, pack.GetAmount(typeof(IronIngot)));
        }
        finally
        {
            Clean();
        }
    }

    [RealMapFact]
    public void WorthOf_TheEngineTailorPaysForShirts_NotOilCloth_AndTheCarpenterForChests()
    {
        // Tailors sewed oil cloth in hundreds of batches and carpenters made writing tables: no
        // vendor's sell table in the era has either.
        IShopSellInfo[] tailor = [new SBTailor().SellInfo];
        IShopSellInfo[] carpenter = [new SBCarpenter().SellInfo];

        Assert.True(CraftBuyers.WorthOf(typeof(FancyShirt), tailor) > 0);
        Assert.Equal(0, CraftBuyers.WorthOf(typeof(OilCloth), tailor));
        Assert.True(CraftBuyers.WorthOf(typeof(WoodenChest), carpenter) > 0);
        Assert.Equal(0, CraftBuyers.WorthOf(typeof(WritingTable), carpenter));
    }

    [RealMapFact]
    public void WorthOf_GearPeopleWantHasWorthWithNoCounterInReach()
    {
        // Fighters buy a smith's plate at the bank; no counter needs to stand by the forge.
        IShopSellInfo[] noCounter = [];

        Assert.True(CraftBuyers.WorthOf(typeof(PlateChest), noCounter) > 0);
        Assert.Equal(0, CraftBuyers.WorthOf(typeof(OilCloth), noCounter));
    }

    [RealMapFact]
    public void MakesSample_OnlyForPlainItems_NotTheTinkersTrapEntries()
    {
        // The live console printed "There is no constructor for DartTrapCraft" for each trap entry.
        Assert.True(CraftBuyers.MakesSample(typeof(FancyShirt)));
        Assert.False(CraftBuyers.MakesSample(typeof(DartTrapCraft)));
        Assert.False(CraftBuyers.MakesSample(typeof(BaseWeapon)));
        Assert.Equal(0, CraftBuyers.WorthOf(typeof(DartTrapCraft), []));
    }

    private void Place(Item item, Point3D at)
    {
        World.AddEntity(item);
        _made.Add(item);
        item.MoveToWorld(at, RealMapWorld.Felucca);
    }

    private T InPack<T>(Container pack, T item) where T : Item
    {
        World.AddEntity(item);
        _made.Add(item);
        pack.AddItem(item);
        return item;
    }

    private void Clean()
    {
        foreach (var entity in _made)
        {
            entity.Delete();
        }

        _made.Clear();
    }
}
