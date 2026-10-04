using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Spawning;

namespace SosariaAI.Skills;

public sealed class BankDepositSkill : Skill
{
    private static readonly ILogger logger = SosariaLog.For(typeof(BankDepositSkill));

    public static readonly TimeSpan BankPause = TimeSpan.FromSeconds(2);

    private const string WalkFailedWhy = "the walk to the bank failed";
    private const string CriminalWhy = "the banker will not serve a criminal";
    private const string NoBankerWhy = "no banker works at this bank";
    private const string NoStaffedBankWhy = "no bank it may use has a banker";
    private const string NoCounterWalkWhy = "no walk to the counter";
    private const string OutOfHearingWhy = "the banker could not hear it from where it stood";
    private const string RefusedWhy = "the banker would not open the box";
    private const string NoBankWalkWhy = "no walk to the bank";

    /// <summary>Every bank of the catalog is a candidate; the catalog lists a few dozen.</summary>
    private const int AllBanks = int.MaxValue;

    private readonly Point3D _bankSpot;
    private Point3D _resolvedSpot;
    private SosariaCharacter _character;
    private TravelSkill _walk;
    private Skill _toBanker;
    private DateTime _arrivedAt;
    private bool _waiting;
    private int _counterSteps;

    public BankDepositSkill(Point3D bankSpot) =>
        _bankSpot = bankSpot == Point3D.Zero ? CharactersFile.DefaultBankSpot : bankSpot;

    public override string Name => SkillKinds.BankDeposit;

    public int ItemsDeposited { get; private set; }

    public int GoldDeposited { get; private set; }

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        ItemsDeposited = 0;
        GoldDeposited = 0;
        _waiting = false;
        _toBanker = null;
        _counterSteps = 0;

        if (character.Criminal)
        {
            return CannotStart(CriminalWhy);
        }

        var murderer = PkRules.IsRed(character.Kills);
        var catalog = NavWorld.DestinationsFor(character.HomeFacet);
        var planned = BankFor(
            WorkSites.WalkTarget(_bankSpot, character.CharacterId, character.HomeSpot),
            murderer,
            murderer ? catalog?.NearestBank(character.Location, murderer) : null
        );
        _resolvedSpot = BankDepositRules.StaffedBank(
            planned,
            UsableBanks(catalog, character.Location, murderer),
            spot => BankTeller.BankerWorksNear(character.Map, spot)
        );

        if (_resolvedSpot == Point3D.Zero)
        {
            return CannotStart(NoStaffedBankWhy);
        }

        // Already inside the bank area: the scattered walk tile is a courtesy, not a
        // requirement. Skip the leg and go straight to the teller.
        if (AtBankSpot(character.Location, _resolvedSpot))
        {
            _walk = null;
            _arrivedAt = Core.Now;
            _waiting = true;
            return true;
        }

        _walk = new TravelSkill(_resolvedSpot, NavLimits.BankArrivalRange);
        return _walk.Begin(character) || CannotStart(NoBankWalkWhy);
    }

    public override SkillStatus Tick()
    {
        if (_walk != null)
        {
            var walkStatus = _walk.Tick();

            if (walkStatus == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _walk = null;

            // The leg fails when its scattered tile sits inside the counter row while
            // the character already stands in the bank. Bank presence is what counts;
            // a walk that truly ends away from the bank still fails.
            if (!AtBankSpot(_character.Location, _resolvedSpot))
            {
                return Fail(WalkFailedWhy);
            }

            _arrivedAt = Core.Now;
            _waiting = true;
        }

        if (_waiting)
        {
            if (Core.Now - _arrivedAt < BankPause)
            {
                return SkillStatus.Running;
            }

            _waiting = false;

            if (!_character.MayUseThisBank())
            {
                return SkillStatus.Done;
            }

            return AtCounter();
        }

        if (_toBanker != null)
        {
            if (_toBanker.Tick() == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            // Crowded or not, the next look at the counter comes after a short wait.
            _toBanker = null;
            _arrivedAt = Core.Now;
            _waiting = true;
            return SkillStatus.Running;
        }

        return SkillStatus.Done;
    }

    /// <summary>
    /// The box opens only where the banker hears. Out of its hearing the person steps up to
    /// the counter, and once more after a wait, before it gives the visit up.
    /// </summary>
    private SkillStatus AtCounter()
    {
        var banker = BankTeller.BankerOnFloor(_character);

        switch (BankTellerRules.AtCounter(
                    _character.Criminal,
                    BankTeller.FindBanker(_character) != null,
                    banker != null,
                    _counterSteps
                ))
        {
            case CounterTurn.Open:
                return Transact();
            case CounterTurn.Criminal:
                return Fail(CriminalWhy);
            case CounterTurn.NoBanker:
                return Fail(NoBankerWhy);
            case CounterTurn.OutOfHearing:
                return Fail(OutOfHearingWhy);
        }

        _counterSteps++;
        _toBanker = BankTeller.WalkToBanker(_character, banker);
        return _toBanker != null ? SkillStatus.Running : Fail(NoCounterWalkWhy);
    }

    // At the counter: "bank", take the lost kit out of the spare kit bag and put carried
    // spares in before the rest of the loot is banked or tossed, drag the goods and the
    // surplus gold in, draw walking money if short, and take the supply reserve out of the box.
    private SkillStatus Transact()
    {
        if (!BankTeller.OpenBox(_character))
        {
            return Fail(RefusedWhy);
        }

        // What the pack beast still carries after the shops goes into the box with the rest.
        PackAnimals.Unload(_character);
        SpareKit.AtCounter(_character);
        Deposit();
        JunkLeftovers();
        GoldDeposited = BankTeller.DepositSurplus(_character);
        BankTeller.WithdrawShortfall(_character);
        SupplyCheck.TakeFromBank(_character);
        SayLootLine();
        EndLeaderTrip();
        return SkillStatus.Done;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _toBanker?.Abort();
        _toBanker = null;
        _waiting = false;
    }

    public override void Resume(TimeSpan held)
    {
        _arrivedAt = SkillClock.Shift(_arrivedAt, held);
        _walk?.Resume(held);
    }

    private void EndLeaderTrip()
    {
        if (!LeadsActiveTrip(_character))
        {
            return;
        }

        Party.Disband(Party.FindByMember(_character.CharacterId).Id);
    }

    private static bool LeadsActiveTrip(SosariaCharacter character)
    {
        var party = character == null ? null : Party.FindByMember(character.CharacterId);
        return party is { TripActive: true } && Party.IsLeader(party.Id, character.CharacterId);
    }

    private void Deposit()
    {
        var bank = _character.BankBox;

        if (bank == null)
        {
            return;
        }

        var moving = BankablePieces(_character);

        // The box takes what fits under its item cap, stacked onto what it holds; the rest
        // stays in the pack.
        foreach (var item in moving)
        {
            if (bank.TryDropItem(_character, item, false))
            {
                ItemsDeposited++;
            }
        }

        var kept = moving.Count - ItemsDeposited;

        if (kept > 0 && SosariaSettings.LogActivity)
        {
            logger.Information("{Name} keeps {Count} pieces in the pack: the bank box is full", _character.Name, kept);
        }
    }

    /// <summary>
    /// What no shop bought and no box wants goes on the floor, the way a player tosses the
    /// vendor-trash sword after the run: plain gear only, never a kit piece, a supply, the
    /// spare weapon kept in the pack, or the pieces a crafter holds back to hawk
    /// (<see cref="HawkerGoods.KeptToHawk"/>). Crafters tossed 212 of their own pieces on the
    /// bank floor in one evening, just before their bank trade, and no fighter ever bought gear
    /// from one.
    /// </summary>
    private void JunkLeftovers()
    {
        var pack = _character.Backpack;
        var dropped = 0;
        var hawked = HawkerGoods.KeptToHawk(_character, CraftMarket.TradeOf(_character));

        foreach (var item in new List<Item>(pack?.Items ?? []))
        {
            if (!IsTossable(_character, item, hawked))
            {
                continue;
            }

            item.MoveToWorld(_character.Location, _character.Map);
            dropped++;
        }

        if (dropped > 0 && SosariaSettings.LogActivity)
        {
            logger.Information("{Name} tossed {Count} unwanted loot pieces at the bank", _character.Name, dropped);
        }
    }

    /// <summary>
    /// True when this pack item is plain gear the character tosses at the bank: not a kit piece,
    /// not blessed, not the spare weapon and not one of the <paramref name="hawked"/> pieces.
    /// </summary>
    internal static bool IsTossable(SosariaCharacter character, Item item, ISet<Item> hawked) =>
        PawnPack.IsJunkable(item) && item.Movable && item.Parent != character &&
        item.LootType is not (LootType.Blessed or LootType.Newbied) && !hawked.Contains(item) &&
        !WorkerTools.IsKitItem(character, item) &&
        !ReferenceEquals(item, GearEquip.BestPackWeapon(character, allowRanged: true));

    /// <summary>
    /// True when a trip to the bank would do something for this character. The box holding
    /// supplies it ran low on, or spare kit pieces it lost (<see cref="SpareKit.PiecesToTake"/>),
    /// is reason enough: a red back from a death with an empty pack skipped the bank.
    /// </summary>
    public static bool HasErrand(SosariaCharacter character) =>
        BankDepositRules.IsWorthATrip(
            PiecesTheBoxTakes(character),
            SupplyCheck.InBankBox(character) + SpareKit.PiecesToTake(character).Count,
            BankTeller.PurseNeedsBanker(character),
            LeadsActiveTrip(character),
            character?.Criminal == true
        );

    /// <summary>
    /// The bankable pack pieces the box would take now (<see cref="BoxTakes"/>). A piece the box
    /// refuses is no reason for a trip: it stays in the pack for a shop, the carry limit, or a
    /// box with room.
    /// </summary>
    private static int PiecesTheBoxTakes(SosariaCharacter character)
    {
        var bank = character?.BankBox;
        var count = 0;

        foreach (var item in BankablePieces(character))
        {
            if (BoxTakes(bank, character, item))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// True when <paramref name="box"/> takes the piece as <see cref="Container.TryDropItem(Mobile, Item, bool)"/>
    /// would: onto a pile it stacks with whatever the item cap, else as a new entry under the
    /// cap, where a bag counts with everything in it. The count of free slots alone would send a
    /// bag of three to a box with one slot left on every visit.
    /// </summary>
    internal static bool BoxTakes(Container box, Mobile owner, Item item)
    {
        if (box == null || item == null)
        {
            return false;
        }

        foreach (var held in box.Items)
        {
            if (held is not Container && held.CanStackWith(item))
            {
                return box.CheckHold(owner, item, false, false);
            }
        }

        return box.CheckHold(owner, item, false, true);
    }

    /// <summary>The banks the character may use, nearest first.</summary>
    private static List<Point3D> UsableBanks(DestinationCatalog catalog, Point3D from, bool murderer)
    {
        var usable = new List<Point3D>();

        foreach (var bank in catalog?.NearestFirst(BankTeller.BankToken, from, AllBanks) ?? [])
        {
            if (PkRules.MayBankAt(murderer, bank.Arrival.X, bank.Arrival.Y))
            {
                usable.Add(bank.Arrival);
            }
        }

        return usable;
    }

    /// <summary>
    /// The bank a trip walks to. A murderer cannot use a guarded town's bank: the walk there
    /// is refused as a walk into the guards, and the teller would not serve it. It walks to
    /// <paramref name="allowedBank"/> instead, the nearest teller it may use, in Buccaneer's Den.
    /// </summary>
    public static Point3D BankFor(Point3D planned, bool murderer, Destination allowedBank) =>
        PkRules.MayBankAt(murderer, planned.X, planned.Y) || allowedBank == null ? planned : allowedBank.Arrival;

    /// <summary>
    /// True when the character stands inside the bank area around the resolved walk
    /// target — close enough to use the teller whether or not the walk leg finished.
    /// </summary>
    public static bool AtBankSpot(Point3D location, Point3D resolvedSpot) =>
        NavMetric.Chebyshev(location, resolvedSpot) <= MeetingRules.BankQuietRange;

    private void SayLootLine()
    {
        if (GoldDeposited < SosariaCombat.LootGoldSayThreshold)
        {
            return;
        }

        var line = _character.Persona?.PickLootLine();

        if (!string.IsNullOrEmpty(line))
        {
            _character.SpeakAloud(line);
        }
    }

    /// <summary>The pack pieces that go into the box (<see cref="IsBankable"/>); none without a pack.</summary>
    private static List<Item> BankablePieces(SosariaCharacter character)
    {
        var pieces = new List<Item>();
        var pack = character?.Backpack;

        if (pack == null)
        {
            return pieces;
        }

        var buyerInReach = SaleGoods.BuyerInReach(character);
        var trade = CraftMarket.TradeOf(character);

        foreach (var item in pack.Items)
        {
            if (IsBankable(character, item, buyerInReach, trade))
            {
                pieces.Add(item);
            }
        }

        return pieces;
    }

    /// <summary>
    /// True when this pack item goes into the box. <paramref name="buyerInReach"/> says whether
    /// a live shop in reach buys an item (<see cref="SaleGoods.BuyerInReach"/>);
    /// <paramref name="trade"/> is the owner's station trade, whose stock stays in the pack.
    /// </summary>
    internal static bool IsBankable(SosariaCharacter character, Item item, Func<Item, bool> buyerInReach, CraftTrade trade)
    {
        if (item == null || item.Deleted || item.Parent == character || item is Gold)
        {
            return false;
        }

        // A crafter keeps the stock its trade burns, as the counter trip keeps it: a tailor
        // that banked its cloth stood at its station out of cloth.
        if (trade?.BurnsStock(item.GetType()) == true)
        {
            return false;
        }

        // The travel kit stays too. Runes, like every supply the supply draw restocks, stay by
        // the supply rule below.
        if (WorkerTools.IsWorkTool(item) || item is Runebook)
        {
            return false;
        }

        // Plain gear gets pawned or tossed; the magic kind and the jewels earn a box slot.
        if (StealRules.KindOf(item) == LootKind.Gear)
        {
            return false;
        }

        // Goods a shop in reach buys wait for the shop. Goods no shop buys, like oak logs, are
        // banked, and so is the plain harvest when no live shop in reach buys it: a miner far
        // from every smith carried its ore for good and could mine no more.
        if (HarvestPack.IsSellable(item) && buyerInReach(item))
        {
            return false;
        }

        if (WorkerTools.IsKitItem(character, item))
        {
            return false;
        }

        // A supply stays: the supply draw would take it straight back out, and the piece left
        // in the pack would send the person to the counter again (HawkerGoods.IsSupply).
        return !HawkerGoods.IsSupply(item);
    }
}
