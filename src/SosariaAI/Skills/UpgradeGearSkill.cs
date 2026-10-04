using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;
using SosariaAI.Spawning;

namespace SosariaAI.Skills;

/// <summary>
/// A shopping trip for one piece of kit. A crafter at a bank in reach holding that piece sells
/// it first: the buyer walks over and haggles for it out loud (<see cref="DealVisit"/>) at the
/// market table's price, and puts it on. Else the buyer walks to the shop that sells it, buys it
/// over the counter, puts it on and sells the old piece back. The piece comes from
/// <see cref="GearPlan"/>: what the person lost first, then the next rung of its armor. A red
/// that wants nothing more for itself buys, a piece a trip, what the spare kit in its bank box
/// lacks, and carries it to the bank (<see cref="SpareKit"/>). A red shops only where a stocked
/// shop stands out of the guards' reach (<see cref="ShopFinder.NearestStocked"/>); a red that
/// finds none buys no gear for <see cref="SpareKitRules.ShopMissRest"/>.
/// </summary>
public sealed class UpgradeGearSkill : Skill
{
    public const string DefaultDestination = ShopFinder.SmithToken;

    /// <summary>What an empty slot reads as in the upgrade line.</summary>
    public const string NothingWorn = "nothing";

    public const string NothingToBuyWhy = "wants no piece it can pay for";
    public const string NoShopWalkWhy = "no walk to a shop that sells the piece";
    public const string ShopWalkFailedWhy = "the walk to the shop failed";
    public const string NotOnShelfWhy = "no shop had the piece";
    public const string NoLegalShopWhy = "no shop out of the guards' reach sells the piece";

    private static readonly ILogger logger = SosariaLog.For(typeof(UpgradeGearSkill));

    /// <summary>When each person last bettered a worn piece. Kept in memory: a reboot is a new day.</summary>
    private static readonly ConditionalWeakTable<SosariaCharacter, StrongBox<DateTime>> LastUpgrade = new();

    private SosariaCharacter _character;
    private TravelSkill _walk;
    private DealVisit _visit;
    private GearOffer _offer;
    private string _shop;
    private string _fallback;
    private bool _buying;
    private bool _bought;
    private bool _red;

    public bool Bought => _bought;

    public override string Name => SkillKinds.UpgradeGear;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _buying = false;
        _bought = false;
        _visit = null;
        _walk = null;
        _red = PkRules.IsRed(character.Kills);
        var offer = OfferFor(character);

        if (offer == null)
        {
            return CannotStart(NothingToBuyWhy);
        }

        _offer = offer.Value;
        _shop = string.IsNullOrWhiteSpace(_offer.VendorDestination) ? DefaultDestination : _offer.VendorDestination;
        _fallback = _red ? null : _offer.FallbackDestination;
        return StartCrafterVisit() || StartShopWalk() || CannotStart(NoShopWhy());
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || !People.InWorld(_character))
        {
            return Fail(LeftWorldReason);
        }

        if (_visit != null)
        {
            return TickVisit();
        }

        if (_walk != null)
        {
            var walkStatus = _walk.Tick();

            if (walkStatus == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            if (walkStatus == SkillStatus.Failed)
            {
                var why = _walk.FailReason ?? ShopWalkFailedWhy;
                return TakeFallback() ? SkillStatus.Running : Fail(why);
            }

            _walk = null;
            _buying = true;
        }

        if (_buying)
        {
            _buying = false;
            TryBuyOnce();

            // A shelf without the piece sends the buyer on to the second shop.
            if (!_bought && TakeFallback())
            {
                return SkillStatus.Running;
            }
        }

        if (_bought)
        {
            return SkillStatus.Done;
        }

        if (_red)
        {
            NoteShopMissed();
        }

        return Fail(NotOnShelfWhy);
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _visit?.Abort();
        _visit = null;
        _buying = false;
    }

    public override void Resume(TimeSpan held)
    {
        _walk?.Resume(held);
        _visit?.Resume(held);
    }

    /// <summary>
    /// The buy this character can make now, or null when it wants nothing it can afford
    /// from the pack above the part of the gold reserve its bank does not hold
    /// (<see cref="GearPlan.PackReserve"/>). A person that wants nothing for itself buys the next
    /// piece its spare kit lacks (<see cref="SpareOfferFor"/>); a red resting after it found no
    /// shop out of the guards' reach (<see cref="SpareKitRules.ShopRests"/>) buys nothing.
    /// </summary>
    public static GearOffer? OfferFor(SosariaCharacter character)
    {
        if (character == null ||
            PkRules.IsRed(character.Kills) && SpareKitRules.ShopRests(character.ClockAt(RuleClock.RedShopMissed), Core.Now))
        {
            return null;
        }

        var gold = character.Backpack?.GetAmount(typeof(Gold)) ?? 0;
        var reserve = PackReserve(character);
        return GearPlan.NextBuy(NeedsOf(character), gold, reserve) ?? SpareOfferFor(character, gold, reserve);
    }

    /// <summary>The gold reserve the pack keeps for this person (<see cref="GearPlan.PackReserve"/>).</summary>
    private static int PackReserve(SosariaCharacter character) =>
        GearPlan.PackReserve(Banker.GetBalance(character), SosariaSettings.GearGoldReserve);

    /// <summary>
    /// The first piece, in kit order, that the spare kit in the bank box and the pack both lack
    /// and a shop sells within the purse (<see cref="GearPlan.SpareOffer"/>), or null. Only a
    /// person with a spare kit bag has one: a red or a blue fighter (<see cref="SpareKitRules.KeepsSpare"/>).
    /// </summary>
    private static GearOffer? SpareOfferFor(SosariaCharacter character, int gold, int reserve)
    {
        var lacking = SpareKit.Lacking(character);

        if (lacking.Count == 0)
        {
            return null;
        }

        var row = ClassBuilds.TemplateOf(character).Weapon;
        var kitWeapon = row == null ? null : KitWeapon(character.Build?.Kit, row);

        for (var i = 0; i < lacking.Count; i++)
        {
            if (GearPlan.SpareOffer(lacking[i], kitWeapon, row, gold, reserve) is { } offer)
            {
                return offer;
            }
        }

        return null;
    }

    /// <summary>What the character wears against what its class, tier and purse call for.</summary>
    public static GearNeeds NeedsOf(SosariaCharacter character)
    {
        var profile = character.PersonProfile;
        var template = ClassBuilds.TemplateOf(character);
        var ranks = new Dictionary<GearSlot, int>();

        foreach (var slot in Enum.GetValues<GearSlot>())
        {
            ranks[slot] = GearScore.SlotRank(character, slot);
        }

        var outer = character.FindItemOnLayer(Layer.OuterTorso);
        var robeWorn = outer is Robe and not DeathRobe;

        return new GearNeeds
        {
            Weight = template.Armor,
            Target = GearLadder.Target(template, profile.Tier, profile.Wealth),
            Female = character.Female,
            Weapon = template.Weapon == null ? null : KitWeapon(character.Build?.Kit, template.Weapon),
            WeaponRow = template.Weapon,
            Armed = HasOwnWeapon(character),
            Shield = template.Shield ? ClassKits.ShieldFor(profile.Tier, character.CharacterId) : null,
            ShieldWorn = character.FindItemOnLayer(Layer.TwoHanded) is BaseShield,
            WantsRobe = !robeWorn && OutfitRules.WearsRobe(profile, character.CharacterId),
            RobeWorn = robeWorn,
            WornRanks = ranks,
            MayUpgrade = !LastUpgrade.TryGetValue(character, out var last) || Core.Now - last.Value >= GearPlan.UpgradeInterval
        };
    }

    /// <summary>The weapon the kit rolled from the row, or the row's own first piece.</summary>
    public static string KitWeapon(IReadOnlyList<string> kit, string row)
    {
        for (var i = 0; i < (kit?.Count ?? 0); i++)
        {
            if (KitVariation.InRow(row, kit[i]))
            {
                return kit[i];
            }
        }

        return row;
    }

    private static bool HasOwnWeapon(Mobile mobile) =>
        mobile.FindItemOnLayer(Layer.OneHanded) is BaseWeapon ||
        mobile.FindItemOnLayer(Layer.TwoHanded) is BaseWeapon ||
        GearEquip.BestPackWeapon(mobile, allowRanged: true) != null;

    /// <summary>
    /// Starts the visit to a crafter at a bank in reach whose pack holds the piece, priced within
    /// the purse above the gold reserve. False when none does.
    /// </summary>
    private bool StartCrafterVisit()
    {
        _visit = CrafterVisit(_character, _offer);
        return _visit != null;
    }

    /// <summary>
    /// The buyer's visit to a crafter holding goods up at a bank in reach whose pack holds a piece
    /// that answers <paramref name="offer"/>, or null. The purse is the one the buyer has on the
    /// bank floor, bank balance counted (<see cref="TradeHandOff.PurseAtBank"/>): the deal settles
    /// beside the banker. Judged by its pack alone, a fighter with its loot banked met few of the
    /// crafters' asks, and 3 of 329 upgrades of a night came from a crafter.
    /// </summary>
    internal static DealVisit CrafterVisit(SosariaCharacter buyer, GearOffer offer)
    {
        var purse = TradeHandOff.PurseAtBank(buyer) - SosariaSettings.GearGoldReserve;

        return TradeMarket.CrafterPieceFor(buyer, item => GearPlan.Fits(offer, item.GetType().Name), BankStockRules.BankReach, purse)
            is { } pick
            ? DealVisit.Start(buyer, pick.Crafter, pick.Piece, pick.Asking)
            : null;
    }

    // The crafter's piece, or on the way to the shop when the haggle fell through.
    private SkillStatus TickVisit()
    {
        var visit = _visit;
        var status = visit.Tick(Core.Now);

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _visit = null;

        if (status != SkillStatus.Done)
        {
            return StartShopWalk() ? SkillStatus.Running : Fail(NoShopWhy());
        }

        var old = Wear(_offer, visit.Goods);

        if (_offer.Kind == GearBuyKind.Spare)
        {
            LogCrafterSpare(visit);
        }
        else
        {
            LogCrafterBuy(old, visit);
        }

        Talk.Maybe(
            _character,
            TalkCategory.GearFromCrafter,
            TalkOdds.CraftDonePercent,
            new TalkSlots { Name = visit.Seller.Name, Item = Appraisal.NounOf(visit.Goods) }
        );
        return SkillStatus.Done;
    }

    /// <summary>
    /// The walk to the shop: for a red, to the nearest shop of either kind with a vendor who
    /// stocks the piece, out of the guards' reach; for anyone else, to the first kind, then the
    /// second.
    /// </summary>
    private bool StartShopWalk()
    {
        if (!_red)
        {
            return StartWalk(_shop) || TakeFallback();
        }

        var types = TypesOf(_offer);
        IReadOnlyList<string> kinds = string.IsNullOrWhiteSpace(_offer.FallbackDestination) ? [_shop] : [_shop, _offer.FallbackDestination];
        return types.Count > 0 && ShopFinder.NearestStocked(_character, kinds, types) is { } shop &&
               Walk(new TravelSkill(shop.Marker, NavLimits.ShopArrivalRange));
    }

    /// <summary>
    /// Why no shop walk started. A red that found no shop out of the guards' reach rests from
    /// shopping (<see cref="NoteShopMissed"/>).
    /// </summary>
    private string NoShopWhy()
    {
        if (!_red)
        {
            return NoShopWalkWhy;
        }

        NoteShopMissed();
        return NoLegalShopWhy;
    }

    private bool StartWalk(string destination) => Walk(new TravelSkill(destination, NavLimits.ShopArrivalRange));

    private bool Walk(TravelSkill walk)
    {
        _walk = walk;

        if (_walk.Begin(_character))
        {
            return true;
        }

        _walk = null;
        return false;
    }

    // A red that found no shelf out of the guards' reach for its piece buys no gear for a while. Logged.
    private void NoteShopMissed()
    {
        _character.StartClock(RuleClock.RedShopMissed, Core.Now);

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} found no shop out of the guards' reach with a {Piece} on its shelf and buys no gear for {Minutes} minutes",
                _character.Name,
                _offer.ItemTypeName,
                (int)SpareKitRules.ShopMissRest.TotalMinutes
            );
        }
    }

    private bool TakeFallback()
    {
        var fallback = _fallback;
        _fallback = null;
        return !string.IsNullOrWhiteSpace(fallback) && StartWalk(fallback);
    }

    // Over the counter: the piece must be on the vendor's shelf and the pack must pay.
    private void TryBuyOnce()
    {
        var offer = OfferFor(_character);
        var pack = _character.Backpack;

        if (offer == null || pack == null)
        {
            return;
        }

        var types = TypesOf(offer.Value);

        if (types.Count == 0)
        {
            return;
        }

        var vendors = VendorDeal.VendorsNear(_character, VendorDeal.CounterRange);
        var goldBefore = pack.GetAmount(typeof(Gold));
        var spendable = goldBefore - PackReserve(_character);
        var carried = PiecesOf(pack, types);

        if (VendorDeal.Buy(_character, vendors, types, 1, spendable) <= 0 || NewPiece(pack, types, carried) is not { } bought)
        {
            return;
        }

        var old = Wear(offer.Value, bought);
        var spent = goldBefore - pack.GetAmount(typeof(Gold));

        if (offer.Value.Kind == GearBuyKind.Spare)
        {
            LogSpare(bought, spent);
            return;
        }

        LogUpgrade(old, bought, spent);
        SellOld(vendors, old);
    }

    /// <summary>
    /// Puts the bought piece on and notes an upgrade. Returns the piece it replaced, or null. A
    /// spare for the kit in the bank box stays in the pack until the next bank visit puts it in
    /// the bag (<see cref="SpareKit.AtCounter"/>).
    /// </summary>
    private Item Wear(GearOffer offer, Item bought)
    {
        _bought = true;

        if (offer.Kind == GearBuyKind.Spare)
        {
            return null;
        }

        var old = WornFor(offer);
        GearEquip.EquipOrPack(_character, bought);

        if (offer.Upgrade)
        {
            LastUpgrade.AddOrUpdate(_character, new StrongBox<DateTime>(Core.Now));
        }

        return old;
    }

    private static List<Type> TypesOf(GearOffer offer)
    {
        var types = new List<Type>();
        var first = AssemblyHandler.FindTypeByName(offer.ItemTypeName);
        var second = AssemblyHandler.FindTypeByName(offer.AlternateTypeName);

        if (first != null)
        {
            types.Add(first);
        }

        if (second != null && second != first)
        {
            types.Add(second);
        }

        return types;
    }

    private static HashSet<Item> PiecesOf(Container pack, List<Type> types)
    {
        var pieces = new HashSet<Item>();

        for (var i = 0; i < types.Count; i++)
        {
            foreach (var item in pack.FindItemsByType(types[i]))
            {
                pieces.Add(item);
            }
        }

        return pieces;
    }

    /// <summary>The piece that came over the counter, not one the pack already carried.</summary>
    private static Item NewPiece(Container pack, List<Type> types, HashSet<Item> carried)
    {
        foreach (var item in PiecesOf(pack, types))
        {
            if (!carried.Contains(item))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>The worn piece the offer replaces, or null when its place is empty.</summary>
    private Item WornFor(GearOffer offer) =>
        offer.Kind switch
        {
            GearBuyKind.Armor when offer.Slot is { } slot => _character.FindItemOnLayer(GearScore.LayerOf(slot)),
            GearBuyKind.Shield => _character.FindItemOnLayer(Layer.TwoHanded),
            GearBuyKind.Robe => _character.FindItemOnLayer(Layer.OuterTorso),
            _ => null
        };

    private void LogUpgrade(Item old, Item bought, int spent)
    {
        if (!SosariaSettings.LogActivity)
        {
            return;
        }

        logger.Information(
            "{Name} upgraded {Old} to {New} for {Gold} gold at {Location}",
            _character.Name,
            old?.GetType().Name ?? NothingWorn,
            bought.GetType().Name,
            spent,
            _character.Location
        );
    }

    private void LogSpare(Item piece, int spent)
    {
        if (!SosariaSettings.LogActivity)
        {
            return;
        }

        logger.Information(
            "{Name} bought a spare {Piece} for {Gold} gold at {Location} for the spare kit in its bank box",
            _character.Name,
            piece.GetType().Name,
            spent,
            _character.Location
        );
    }

    private void LogCrafterSpare(DealVisit visit)
    {
        if (!SosariaSettings.LogActivity)
        {
            return;
        }

        logger.Information(
            "{Name} bought a spare {Piece} for {Gold} gold from the {Trade} crafter {Crafter} at the bank at {Location} for the spare kit in its bank box",
            _character.Name,
            visit.Goods.GetType().Name,
            visit.Price,
            CraftMarket.TradeOf(visit.Seller)?.Kind,
            visit.Seller.Name,
            _character.Location
        );
    }

    private void LogCrafterBuy(Item old, DealVisit visit)
    {
        if (!SosariaSettings.LogActivity)
        {
            return;
        }

        logger.Information(
            "{Name} upgraded {Old} to {New} for {Gold} gold from the {Trade} crafter {Crafter} at the bank at {Location}",
            _character.Name,
            old?.GetType().Name ?? NothingWorn,
            visit.Goods.GetType().Name,
            visit.Price,
            CraftMarket.TradeOf(visit.Seller)?.Kind,
            visit.Seller.Name,
            _character.Location
        );
    }

    // The old piece is sold back over the same counter when the shop buys it, the way a
    // player cleared the pack after a trip; a piece no shop wants stays in the pack.
    private void SellOld(List<BaseVendor> vendors, Item old)
    {
        if (old == null || old.Deleted || !old.IsChildOf(_character.Backpack) || old is DeathRobe)
        {
            return;
        }

        for (var i = 0; i < vendors.Count; i++)
        {
            if (VendorDeal.Buys(vendors[i], old))
            {
                VendorDeal.Sell(_character, vendors[i], [old]);
                return;
            }
        }
    }
}
