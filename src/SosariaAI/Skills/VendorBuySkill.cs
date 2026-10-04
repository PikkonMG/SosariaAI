using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// A shopping errand for what really ran out: arrows at the bowyer, bandages at the healer,
/// reagents at the mage shop. With no healer's shelf in reach holding bandages, the bandages
/// come from cloth bought at a tailor or a weaver and cut on the spot (<see cref="ClothBandages"/>).
/// With every supply stocked, a lost harvest tool is bought where the engine sells it, then
/// pet food (<see cref="PetPantry"/>), then a gatherer's pack beast at the animal trainer,
/// then a lost kit piece at a shop of its kind. Only a shop where a live vendor stands who stocks the want
/// is picked (<see cref="ShopFinder.NearestStocked"/>), and the walk goes to that vendor.
/// Only what the vendor stocks is bought, at its price, out of the pack. The errand is
/// planned once for the scorer and again at the start, so the scorer never offers a trip
/// with nothing to buy. A supply errand that finds the shelves empty goes on to the bank and
/// asks the floor for the supplies (<see cref="BankShopSkill.ForSupplies"/>) before it fails.
/// </summary>
public sealed class VendorBuySkill : Skill
{
    private static readonly ILogger logger = SosariaLog.For(typeof(VendorBuySkill));

    private static readonly string[] ArmorShops = [ShopFinder.SmithToken, ShopFinder.ArmorerToken];
    private static readonly string[] WeaponShops = [ShopFinder.SmithToken, ShopFinder.WeaponsmithToken];
    private static readonly string[] BowShops = [ShopFinder.BowyerToken];
    private static readonly string[] BookShops = [ShopFinder.MageToken];
    private static readonly string[] AnimalShops = [ShopFinder.AnimalTrainerToken];

    private const string NoErrandWhy = "nothing in reach it needs and can pay for";

    private delegate StockedShop? ErrandPlanner(SosariaCharacter character, List<(Type Type, int Amount)> wanted);

    private static readonly ErrandPlanner[] Planners = [PlanSupplies, PlanTools, PetPantry.Plan, PlanBeast, PlanKit];

    private readonly List<(Type Type, int Amount)> _wanted = [];
    private SosariaCharacter _character;
    private TravelSkill _walk;
    private Point3D _marker;
    private BankShopSkill _floor;
    private int _shortBefore;
    private string _shelfWhy;

    public int ItemsBought { get; private set; }

    public override string Name => SkillKinds.VendorBuy;

    /// <summary>
    /// True when the person is short of something a shop in reach sells and carries the gold
    /// for one unit of it there. The scorer offers a shopping trip only then.
    /// </summary>
    public static bool HasErrand(SosariaCharacter character) =>
        character?.Backpack != null && character.Backpack.GetAmount(typeof(Gold)) > 0 &&
        Plan(character, []) != null;

    /// <summary>
    /// True when a supply that stops the fight or that a shop sells ran low, a stocked shop in
    /// reach sells it (or, for bandages, the cloth that makes them), and the person carries the
    /// gold for one unit there. A person low on bandages with no shelf in reach to refill them
    /// fights on with what it has instead of standing in town.
    /// </summary>
    public static bool HasSupplyErrand(SosariaCharacter character) =>
        character?.Backpack != null && character.Backpack.GetAmount(typeof(Gold)) > 0 &&
        Errand(PlanSupplies, character, []) != null;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _walk = null;
        _marker = Point3D.Zero;
        _wanted.Clear();
        _floor = null;
        _shelfWhy = null;
        ItemsBought = 0;

        if (character?.Backpack == null || !People.InWorld(character))
        {
            return CannotStart(NotInWorldReason);
        }

        if (character.Backpack.GetAmount(typeof(Gold)) <= 0)
        {
            return CannotStart("no gold in the pack");
        }

        if (Plan(character, _wanted) is not { } shop)
        {
            return CannotStart(NoErrandWhy);
        }

        _marker = shop.Marker;
        _walk = new TravelSkill(shop.Vendor.Location, NavLimits.ShopArrivalRange, arrivalFloor: true);
        return _walk.Begin(character) || CannotStart("no walk to the shop");
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || !People.InWorld(_character))
        {
            return Fail(LeftWorldReason);
        }

        if (_floor != null)
        {
            return TickFloor();
        }

        var walk = _walk?.Tick() ?? SkillStatus.Failed;

        if (walk == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _walk = null;

        if (walk == SkillStatus.Failed)
        {
            return Fail("the walk to the shop failed");
        }

        BuyWanted();

        if (ItemsBought > 0)
        {
            return SkillStatus.Done;
        }

        var why = NoSaleReason();
        return AskTheFloor(why) ? SkillStatus.Running : Fail(why);
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _floor?.Abort();
        _floor = null;
    }

    public override void Resume(TimeSpan held)
    {
        _walk?.Resume(held);
        _floor?.Resume(held);
    }

    /// <summary>
    /// True when an empty shelf sends the errand on to the bank floor: the want was supplies, a
    /// vendor stood at the shop, and the person is still short.
    /// </summary>
    public static bool AsksTheFloor(bool suppliesWanted, bool vendorAtShop, int shortUnits) =>
        suppliesWanted && vendorAtShop && shortUnits > 0;

    // The shelves are empty: the supplies may be bought off a player at the bank.
    private bool AskTheFloor(string why)
    {
        _shortBefore = SupplyMarket.ShortUnits(_character);

        if (!AsksTheFloor(
                _wanted.Exists(line => SupplyMarket.KindsOf(line.Type).Count > 0 || ClothBandages.IsMaking(line.Type)),
                VendorDeal.VendorsNear(_character, VendorDeal.CounterRange).Count > 0,
                _shortBefore
            ))
        {
            return false;
        }

        var floor = BankShopSkill.ForSupplies();

        if (!floor.Begin(_character))
        {
            return false;
        }

        _floor = floor;
        _shelfWhy = why;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} found the shelves bare ({Why}) and asks at the bank", _character.Name, why);
        }

        return true;
    }

    // Done when the floor sold it any of what it lacked; else the shelf's reason stands.
    private SkillStatus TickFloor()
    {
        var status = _floor.Tick();

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _floor = null;
        return SupplyMarket.ShortUnits(_character) < _shortBefore ? SkillStatus.Done : Fail(_shelfWhy);
    }

    /// <summary>
    /// A worker whose tool broke buys a new one over the counter of the shop it just sold
    /// to, as a player buys a pickaxe with the ore money. Returns the tools bought.
    /// </summary>
    public static int BuyMissingWorkerTools(SosariaCharacter worker, IReadOnlyList<BaseVendor> vendors)
    {
        var bought = 0;

        foreach (var tool in WorkerTools.Missing(worker))
        {
            bought += VendorDeal.Buy(worker, vendors, [tool], 1, worker.Backpack?.GetAmount(typeof(Gold)) ?? 0);
        }

        return bought;
    }

    /// <summary>
    /// Fills <paramref name="wanted"/> and returns the shop to walk to, or null. Low
    /// supplies come first, then a missing harvest tool, then pet food, then a pack beast,
    /// then a lost kit piece. A want no stocked shop in reach sells gives way to the next.
    /// </summary>
    private static StockedShop? Plan(SosariaCharacter character, List<(Type Type, int Amount)> wanted)
    {
        for (var i = 0; i < Planners.Length; i++)
        {
            if (Errand(Planners[i], character, wanted) is { } shop)
            {
                return shop;
            }
        }

        wanted.Clear();
        return null;
    }

    // One planner's errand: its stocked shop in reach when the person can pay for one unit
    // there, with its lines in wanted; else null.
    private static StockedShop? Errand(ErrandPlanner planner, SosariaCharacter character, List<(Type Type, int Amount)> wanted)
    {
        wanted.Clear();
        return planner(character, wanted) is { } shop && PaysForOne(character, shop.Vendor, wanted) ? shop : null;
    }

    /// <summary>
    /// True when the vendor can take the price of one unit (<paramref name="cheapestPrice"/>)
    /// from what the buyer holds (<see cref="PackFunds.MayPay"/>). Connor, with 4 gold, walked to
    /// the healer for 5-gold bandages and failed at the counter, again and again.
    /// </summary>
    public static bool PaysForOne(int cheapestPrice, int packGold, int bankGold) =>
        PackFunds.MayPay(packGold, bankGold, cheapestPrice);

    private static bool PaysForOne(SosariaCharacter character, BaseVendor vendor, List<(Type Type, int Amount)> wanted) =>
        PaysForOne(
            CheapestWanted(vendor, wanted),
            character.Backpack?.GetAmount(typeof(Gold)) ?? 0,
            Banker.GetBalance(character)
        );

    // The lowest unit price on the vendor's shelf among the wanted lines; 0 when it stocks none.
    private static int CheapestWanted(BaseVendor vendor, List<(Type Type, int Amount)> wanted)
    {
        var cheapest = 0;

        for (var i = 0; i < wanted.Count; i++)
        {
            var price = VendorDeal.CheapestPrice([vendor], [wanted[i].Type]);

            if (price > 0 && (cheapest == 0 || price < cheapest))
            {
                cheapest = price;
            }
        }

        return cheapest;
    }

    // A miner or woodcutter without a pack beast buys one at the animal trainer.
    private static StockedShop? PlanBeast(SosariaCharacter character, List<(Type Type, int Amount)> wanted)
    {
        if (!PackAnimals.WantsBeast(character))
        {
            return null;
        }

        var beast = PackAnimals.BeastFor(character);
        var shop = ShopFinder.NearestStocked(character, AnimalShops, [beast]);

        if (shop != null)
        {
            wanted.Add((beast, 1));
        }

        return shop;
    }

    // The most urgent low supply picks the shop; every other low supply that shop sells
    // joins the list. Recall scrolls have no shop: the bank box holds them. Bandages no
    // healer in reach has on its shelf are made of cloth from a tailor or a weaver.
    private static StockedShop? PlanSupplies(SosariaCharacter character, List<(Type Type, int Amount)> wanted)
    {
        var needs = SupplyCheck.LowNeeds(character);
        var first = needs.FindIndex(need => SupplyRules.ShopToken(need.Kind) != null);

        if (first < 0)
        {
            return null;
        }

        var token = SupplyRules.ShopToken(needs[first].Kind);

        for (var i = 0; i < needs.Count; i++)
        {
            if (SupplyRules.ShopToken(needs[i].Kind) == token)
            {
                wanted.AddRange(SupplyCheck.BuyLines(character.Backpack, needs[i]));
            }
        }

        var wares = wanted.ConvertAll(line => line.Type);
        var shop = ShopFinder.NearestStocked(character, [token], wares);
        var alternate = SupplyRules.AlternateShopToken(needs[first].Kind);

        if (shop == null && alternate != null)
        {
            shop = ShopFinder.NearestStocked(character, [alternate], wares);
        }

        if (shop != null || !SupplyRules.MadeFromCloth(needs[first].Kind))
        {
            return shop;
        }

        wanted.Clear();
        return ClothBandages.Plan(character, needs[first], wanted);
    }

    // The first missing tool a stocked shop in reach sells; every other missing tool joins the list.
    private static StockedShop? PlanTools(SosariaCharacter character, List<(Type Type, int Amount)> wanted)
    {
        StockedShop? shop = null;

        foreach (var tool in WorkerTools.Missing(character))
        {
            shop ??= ShopFinder.NearestStocked(character, WorkerTools.ShopsFor(tool), [tool]);
            wanted.Add((tool, 1));
        }

        return shop;
    }

    // Lost kit pieces, each at a shop of its kind: armor at the smith or the armorer, a blade
    // at the smith or the weaponsmith, a bow at the bowyer, a spellbook at the mage. The
    // first kind a stocked shop in reach sells one of picks the shop; every piece of that
    // kind joins it. One search per kind: this runs for the scorer on every score.
    private static StockedShop? PlanKit(SosariaCharacter character, List<(Type Type, int Amount)> wanted)
    {
        var groups = GroupByShops(LostKit(character), ShopsForKit);

        for (var g = 0; g < groups.Count; g++)
        {
            if (ShopFinder.NearestStocked(character, groups[g].Shops, groups[g].Pieces) is { } shop)
            {
                wanted.AddRange(groups[g].Pieces.ConvertAll(piece => (piece, 1)));
                return shop;
            }
        }

        return null;
    }

    /// <summary>
    /// <paramref name="pieces"/> grouped by the shop kinds that sell them, in the order each
    /// kind is first needed. A piece no shop kind sells is left out.
    /// </summary>
    public static List<KitShops> GroupByShops(IReadOnlyList<Type> pieces, Func<Type, IReadOnlyList<string>> shopsFor)
    {
        var groups = new List<KitShops>();

        for (var i = 0; i < pieces.Count; i++)
        {
            if (shopsFor(pieces[i]) is not { Count: > 0 } shops)
            {
                continue;
            }

            var group = groups.FindIndex(existing => ReferenceEquals(existing.Shops, shops));

            if (group < 0)
            {
                groups.Add(new KitShops(shops, [pieces[i]]));
            }
            else
            {
                groups[group].Pieces.Add(pieces[i]);
            }
        }

        return groups;
    }

    // Kit pieces the character no longer carries that a shop sells: supplies and harvest
    // tools have their own errands.
    private static List<Type> LostKit(SosariaCharacter character)
    {
        var kit = character.Build?.Kit;
        var lost = new List<Type>();

        for (var i = 0; i < (kit?.Count ?? 0); i++)
        {
            var type = AssemblyHandler.FindTypeByName(kit[i]);

            if (type != null && typeof(Item).IsAssignableFrom(type) && !IsSupplyGoods(type) &&
                WorkerTools.ShopsFor(type).Count == 0 && !WorkerTools.Carries(character, type))
            {
                lost.Add(type);
            }
        }

        return lost;
    }

    private static IReadOnlyList<string> ShopsForKit(Type type)
    {
        if (typeof(BaseArmor).IsAssignableFrom(type))
        {
            return ArmorShops;
        }

        if (typeof(BaseRanged).IsAssignableFrom(type))
        {
            return BowShops;
        }

        if (typeof(BaseWeapon).IsAssignableFrom(type))
        {
            return WeaponShops;
        }

        return typeof(Spellbook).IsAssignableFrom(type) ? BookShops : null;
    }

    private void BuyWanted()
    {
        var pack = _character.Backpack;

        if (pack == null)
        {
            return;
        }

        var vendors = VendorDeal.VendorsNear(_character, VendorDeal.CounterRange);
        var goldBefore = pack.GetAmount(typeof(Gold));

        for (var i = 0; i < _wanted.Count; i++)
        {
            var (type, amount) = _wanted[i];
            var bought = PackAnimals.IsPackBeast(type)
                ? PackAnimals.Buy(_character, vendors, type) ? 1 : 0
                : BuyAcrossLines(_character, vendors, type, amount);

            if (bought <= 0)
            {
                continue;
            }

            ItemsBought += bought;

            // Only a worn piece is put on: a stack of goods (food for a pet, cloth) never is.
            if (!IsSupplyGoods(type) && pack.FindItemByType(type) is { Stackable: false, Layer: not Layer.Invalid } gear)
            {
                GearEquip.EquipOrPack(_character, gear);
            }
        }

        var goldSpent = Math.Max(0, goldBefore - pack.GetAmount(typeof(Gold)));

        if (ItemsBought > 0 && SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} bought {Count} supplies and kit for {Gold} gold at {Location}",
                _character.Name,
                ItemsBought,
                goldSpent,
                _character.Location
            );
        }

        if (_wanted.Exists(line => ClothBandages.IsMaking(line.Type)))
        {
            ClothBandages.Cut(_character);
        }
    }

    /// <summary>
    /// Buys up to <paramref name="amount"/> units of <paramref name="type"/>, one shelf line after
    /// the next until the want is met or no line sells more: a weaver keeps its uncut cloth and
    /// its bolts on four lines of 20 each, and one deal takes from one line.
    /// </summary>
    private static int BuyAcrossLines(SosariaCharacter buyer, IReadOnlyList<BaseVendor> vendors, Type type, int amount)
    {
        var bought = 0;

        while (bought < amount)
        {
            var got = VendorDeal.Buy(buyer, vendors, [type], amount - bought, buyer.Backpack.GetAmount(typeof(Gold)));

            if (got <= 0)
            {
                break;
            }

            bought += got;
        }

        return bought;
    }

    private string NoSaleReason()
    {
        if (VendorDeal.VendorsNear(_character, VendorDeal.CounterRange).Count == 0)
        {
            ShopFinder.NoteEmpty(_character, _marker);
            return "no vendor at the shop";
        }

        return _wanted.Count == 0
            ? "nothing to buy"
            : $"the vendors sell no {_wanted[0].Type.Name} it can pay for";
    }

    // Goods bought to burn or to work, never worn: the supplies with their own targets (arrows,
    // reagents, bandages) and the cloth and scissors that make bandages.
    private static bool IsSupplyGoods(Type type) =>
        typeof(BaseReagent).IsAssignableFrom(type) || type == typeof(Arrow) || type == typeof(Bolt) ||
        type == typeof(Bandage) || ClothBandages.IsMaking(type);

    /// <summary>The kit pieces one kind of shop sells, and that kind's shop tokens.</summary>
    public readonly record struct KitShops(IReadOnlyList<string> Shops, List<Type> Pieces);
}
