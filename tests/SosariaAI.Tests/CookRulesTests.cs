using Server;
using Server.Items;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CookRulesTests
{
    private const string ExpectedKind = "Cook";
    private const string ExpectedShopToken = "tavern";
    private const string ExpectedMaterialShopToken = "vendor:Butcher";
    private const int BritainInnX = 1427;
    private const int BritainInnY = 1716;
    private const int BritainInnZ = 20;
    private const int CampfireItemId = 0xDE3;
    private const int CampfireItemIdMax = 0xDE9;
    private const int StoneOvenItemId = 0x92B;
    private const int FirepitItemId = 0xFAC;
    private const int NotHeatItemId = 1;
    private const int ElvenStoveItemId = 0x2DDB;
    private const int BrazierItemId = 0x19AA;
    private const int SmallForgeItemId = 0xFB1;
    private const int LargeForgeItemId = 0x197A;
    private const int ElvenForgeItemId = 0x2DD8;
    private const string ExpectedBakerToken = "vendor:Baker";
    private const string ExpectedSmithToken = "blacksmith";
    private const double SomeCamping = 10;

    [Fact]
    public void Trade_CooksAtTheInnHearthWithMeatFromTheButcher()
    {
        var trade = CookRules.Trade;

        Assert.Equal(ExpectedKind, trade.Kind);
        Assert.Equal(SkillName.Cooking, trade.Skill);
        Assert.Equal(ExpectedShopToken, trade.ShopToken);
        Assert.Equal(new Point3D(BritainInnX, BritainInnY, BritainInnZ), trade.FallbackShop);
        Assert.Equal(CraftStation.Heat, trade.Station);
        Assert.Contains(ExpectedMaterialShopToken, trade.SupplyShopTokens);
    }

    [Fact]
    public void IsHeatSourceId_MatchesModernUOHeatSources()
    {
        Assert.True(CookRules.IsHeatSourceId(CampfireItemId));
        Assert.True(CookRules.IsHeatSourceId(CampfireItemIdMax));
        Assert.True(CookRules.IsHeatSourceId(StoneOvenItemId));
        Assert.True(CookRules.IsHeatSourceId(FirepitItemId));
        Assert.False(CookRules.IsHeatSourceId(NotHeatItemId));
    }

    [Fact]
    public void IsHeatSourceId_CountsStovesBraziersAndForgesAsTheEngineDoes()
    {
        Assert.True(CookRules.IsHeatSourceId(ElvenStoveItemId));
        Assert.True(CookRules.IsHeatSourceId(BrazierItemId));
        Assert.True(CookRules.IsHeatSourceId(SmallForgeItemId));
        Assert.True(CookRules.IsHeatSourceId(LargeForgeItemId));
        Assert.True(CookRules.IsHeatSourceId(ElvenForgeItemId));
    }

    [Fact]
    public void Trade_LooksForHeatAtInnsBakeriesAndSmithies()
    {
        // Most inns have no hearth; the Britain inn oven was the exception.
        Assert.Equal([ExpectedShopToken, ExpectedBakerToken, ExpectedSmithToken], CookRules.Trade.StationShopTokens);
    }

    [Fact]
    public void MayLightFire_NeedsKindlingCampingAndOpenGround()
    {
        Assert.True(CookRules.MayLightFire(hasKindling: true, SomeCamping, inDungeon: false));
        Assert.False(CookRules.MayLightFire(hasKindling: false, SomeCamping, inDungeon: false));
        Assert.False(CookRules.MayLightFire(hasKindling: true, CookRules.NoCamping, inDungeon: false));
        Assert.False(CookRules.MayLightFire(hasKindling: true, SomeCamping, inDungeon: true));
    }

    [Fact]
    public void IsHeatSource_Null_IsFalse() =>
        Assert.False(CookRules.IsHeatSource(null));

    [Fact]
    public void Trade_BurnsTheButchersRawMeat()
    {
        Assert.True(CookRules.Trade.BurnsStock(typeof(RawRibs)));
        Assert.True(CookRules.Trade.BurnsStock(typeof(RawBird)));
        Assert.True(CookRules.Trade.BurnsStock(typeof(RawLambLeg)));
        Assert.True(CookRules.Trade.BurnsStock(typeof(RawChickenLeg)));
        Assert.False(CookRules.Trade.BurnsStock(typeof(Ribs)));
    }

    [Fact]
    public void MayCook_NeedsAPanMeatAndHeat_EachCarriedOrForSaleInReach()
    {
        Assert.True(CookRules.MayCook(hasPan: true, panForSale: No, hasMeat: true, meatForSale: No, hasHeat: Yes));
        Assert.True(CookRules.MayCook(hasPan: false, panForSale: Yes, hasMeat: false, meatForSale: Yes, hasHeat: Yes));
        Assert.False(CookRules.MayCook(hasPan: true, panForSale: Yes, hasMeat: true, meatForSale: Yes, hasHeat: No));
    }

    [Fact]
    public void MayCook_NoButcherInReach_NoCooking()
    {
        // Magincia has an inn and a baker but no butcher: its cooks failed for want of meat.
        Assert.False(CookRules.MayCook(hasPan: true, panForSale: Yes, hasMeat: false, meatForSale: No, hasHeat: Yes));
    }

    [Fact]
    public void MayCook_NoPanSoldInReach_NoCooking()
    {
        // A tavern keeper sells no skillet: only the tavern's cook does.
        Assert.False(CookRules.MayCook(hasPan: false, panForSale: No, hasMeat: true, meatForSale: Yes, hasHeat: Yes));
    }

    [Fact]
    public void MayCook_ACookCarryingPanAndMeatLooksAtNoShelf()
    {
        // Each shelf look walks the vendors and the shop list, for every cook on every score.
        var looks = 0;
        bool Shelf()
        {
            looks++;
            return true;
        }

        Assert.True(CookRules.MayCook(hasPan: true, panForSale: Shelf, hasMeat: true, meatForSale: Shelf, hasHeat: Yes));
        Assert.Equal(0, looks);

        // No pan and none for sale: the meat shelf and the heat are never looked at.
        Assert.False(CookRules.MayCook(hasPan: false, panForSale: No, hasMeat: false, meatForSale: Shelf, hasHeat: Shelf));
        Assert.Equal(0, looks);
    }

    private static bool Yes() => true;

    private static bool No() => false;
}
