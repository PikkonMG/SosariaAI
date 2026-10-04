using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class KindFromGroupTests
{
    [Theory]
    [InlineData("bank", "Britain West Bank", DestinationKind.Bank)]
    [InlineData("Banks", "east window", DestinationKind.Bank)]
    [InlineData("Towns", "Minoc Bank", DestinationKind.Bank)]
    [InlineData("healer", "Britain Healer", DestinationKind.Healer)]
    [InlineData("Inns", "The Salty Dog", DestinationKind.Healer)]
    [InlineData("Towns", "Innkeeper", DestinationKind.Healer)]
    [InlineData("Shrines", "Chaos", DestinationKind.Shrine)]
    [InlineData("ankh", "Virtue Ankh", DestinationKind.Shrine)]
    [InlineData("", "Compassion", DestinationKind.Shrine)]
    [InlineData("", "Honesty", DestinationKind.Shrine)]
    [InlineData("", "Honor", DestinationKind.Shrine)]
    [InlineData("", "Humility", DestinationKind.Shrine)]
    [InlineData("", "Justice", DestinationKind.Shrine)]
    [InlineData("", "Sacrifice", DestinationKind.Shrine)]
    [InlineData("", "Spirituality", DestinationKind.Shrine)]
    [InlineData("", "Valor", DestinationKind.Shrine)]
    [InlineData("Dungeons", "Entrance", DestinationKind.Dungeon)]
    [InlineData("Despise", "Level 1", DestinationKind.Dungeon)]
    [InlineData("Deceit", "Level 2", DestinationKind.Dungeon)]
    [InlineData("Destard", "Level 3", DestinationKind.Dungeon)]
    [InlineData("Covetous", "Lake Cave", DestinationKind.Dungeon)]
    [InlineData("Shame", "Level 4", DestinationKind.Dungeon)]
    [InlineData("Wrong", "Entrance", DestinationKind.Dungeon)]
    [InlineData("Hythloth", "Level 1", DestinationKind.Dungeon)]
    [InlineData("Fire", "Brit Entrance", DestinationKind.Dungeon)]
    [InlineData("Ice", "Ice Demon Lair", DestinationKind.Dungeon)]
    [InlineData("Khaldun", "Entrance 1", DestinationKind.Dungeon)]
    [InlineData("Orc Cave", "Level 1", DestinationKind.Dungeon)]
    [InlineData("Outdoors", "Orc Camp", DestinationKind.Dungeon)]
    [InlineData("mine", "Mountain Mine", DestinationKind.Resource)]
    [InlineData("ore", "Vein", DestinationKind.Resource)]
    [InlineData("mountain", "Pass", DestinationKind.Resource)]
    [InlineData("lumber", "Yew Woods", DestinationKind.Resource)]
    [InlineData("forest", "Britain Forest", DestinationKind.Resource)]
    [InlineData("tree", "Oak Stand", DestinationKind.Resource)]
    [InlineData("fish", "River", DestinationKind.Resource)]
    [InlineData("shore", "Docks", DestinationKind.Resource)]
    [InlineData("Graveyards", "Britain Cemetery", DestinationKind.Hunt)]
    [InlineData("hunt", "Boar Run", DestinationKind.Hunt)]
    [InlineData("Outdoors", "Deer", DestinationKind.Hunt)]
    [InlineData("WildLife", "Wolf", DestinationKind.Hunt)]
    [InlineData("troll", "Bridge", DestinationKind.Hunt)]
    [InlineData("Vendors", "Britain Market", DestinationKind.Vendor)]
    [InlineData("Towns", "Blacksmith", DestinationKind.Vendor)]
    [InlineData("mage", "Moonglow Tower", DestinationKind.Vendor)]
    [InlineData("tailor", "Cloth Shop", DestinationKind.Vendor)]
    [InlineData("provisioner", "General Shop", DestinationKind.Vendor)]
    [InlineData("tinker", "Workbench", DestinationKind.Vendor)]
    [InlineData("Towns", "Carpenter", DestinationKind.Vendor)]
    [InlineData("Towns", "Fisherman", DestinationKind.Vendor)]
    [InlineData(null, "Bowyer", DestinationKind.Vendor)]
    [InlineData(null, "Mapmaker", DestinationKind.Vendor)]
    [InlineData("bowyer", "Quality Fletching", DestinationKind.Vendor)]
    [InlineData("fletcher", "Nujel'm Bowry", DestinationKind.Vendor)]
    [InlineData("tavern", "Salty Dog", DestinationKind.Vendor)]
    [InlineData("baker", "A Loaf", DestinationKind.Vendor)]
    [InlineData("arms", "Blades", DestinationKind.Vendor)]
    [InlineData("stable", "Britannia Stables", DestinationKind.Vendor)]
    [InlineData(null, "Jeweler", DestinationKind.Vendor)]
    [InlineData(null, "TavernKeeper", DestinationKind.Vendor)]
    [InlineData(null, "AnimalTrainer", DestinationKind.Vendor)]
    [InlineData("Towns", "Britain", DestinationKind.Vendor)]
    [InlineData("TownsLife", "Guard", DestinationKind.Vendor)]
    [InlineData("SeaLife", "Shark", DestinationKind.Hunt)]
    [InlineData("reagents", "Herbal Splendor", DestinationKind.Vendor)]
    [InlineData("", "", DestinationKind.Hunt)]
    public void From_MapsGroupAndName(string group, string name, DestinationKind expected) =>
        Assert.Equal(expected, KindFromGroup.From(group, name));

    [Fact]
    public void From_IsCaseInsensitive()
    {
        Assert.Equal(DestinationKind.Bank, KindFromGroup.From("BANK", "EAST"));
        Assert.Equal(DestinationKind.Dungeon, KindFromGroup.From("dEsPiSe", "entry"));
        Assert.Equal(DestinationKind.Shrine, KindFromGroup.From("SHRINES", "chaos"));
        Assert.Equal(DestinationKind.Healer, KindFromGroup.From("INN", "room"));
    }

    [Fact]
    public void From_OrcIsDungeonNotHunt() =>
        Assert.Equal(DestinationKind.Dungeon, KindFromGroup.From("orc fort", "camp"));

    [Fact]
    public void From_JusticeIsShrineNotIceDungeon() =>
        Assert.Equal(DestinationKind.Shrine, KindFromGroup.From("", "Justice"));

    [Fact]
    public void From_NameBank_WhenGroupHasNoToken() =>
        Assert.Equal(DestinationKind.Bank, KindFromGroup.From("Places", "West Bank"));

    [Fact]
    public void From_TownDefault_IsVendor() =>
        Assert.Equal(DestinationKind.Vendor, KindFromGroup.From("Towns", "Center"));

    [Fact]
    public void From_SpawnAreaDefault_IsHunt() =>
        Assert.Equal(DestinationKind.Hunt, KindFromGroup.From("LostLands", "Camp"));
}
