using System;
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
/// Banking aloud at a real banker. The words carry the banker's own keywords, so "bank"
/// opens the box and "withdraw 2000" moves two thousand gold through Banker.OnSpeech with
/// the banker's rules: no business with criminals and the era's withdrawal ceiling. Gold
/// going in is dragged into the opened box. An amount is only named when it is there.
/// </summary>
public static class BankTeller
{
    private static readonly ILogger logger = SosariaLog.For(typeof(BankTeller));

    /// <summary>The destination token every bank in the catalog answers to.</summary>
    public const string BankToken = "bank";

    /// <summary>
    /// True when a live banker works on the bank floor around <paramref name="spot"/>
    /// (<see cref="BankTellerRules.BankerWalkRange"/>): a catalog bank with no live banker is
    /// no bank.
    /// </summary>
    public static bool BankerWorksNear(Map map, Point3D spot)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        foreach (var mobile in map.GetMobilesInRange(spot, BankTellerRules.BankerWalkRange))
        {
            if (mobile is Banker { Deleted: false, Alive: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A banker close enough to hear the person, or null.</summary>
    public static Banker FindBanker(Mobile person) => FindBanker(person, BankTellerRules.BankerHearing);

    /// <summary>The nearest banker within <paramref name="range"/>, or null.</summary>
    public static Banker FindBanker(Mobile person, int range)
    {
        if (!People.InWorld(person))
        {
            return null;
        }

        Banker nearest = null;
        var best = double.MaxValue;

        foreach (var mobile in person.Map.GetMobilesInRange(person.Location, range))
        {
            if (mobile is Banker { Deleted: false } banker && person.GetDistanceToSqrt(banker) < best)
            {
                best = person.GetDistanceToSqrt(banker);
                nearest = banker;
            }
        }

        return nearest;
    }

    /// <summary>The banker nearest the person on its bank floor (<see cref="BankTellerRules.BankerWalkRange"/>), or null.</summary>
    public static Banker BankerOnFloor(Mobile person) => FindBanker(person, BankTellerRules.BankerWalkRange);

    /// <summary>
    /// A walk up to the counter of <paramref name="banker"/>, tile by tile across the bank
    /// floor, or null when no walk starts. A walk that ended on the plaza twenty tiles out
    /// reaches the banker from there.
    /// </summary>
    public static Skill WalkToBanker(SosariaCharacter person, Banker banker)
    {
        var map = person?.Map;

        if (map == null || map == Map.Internal || banker is not { Deleted: false })
        {
            return null;
        }

        var tiles = TileRoute.Find(
            person.Location,
            banker.Location,
            Standable.Walker(map),
            (x, y, z) => IndoorTiles.IsBuilding(map, x, y, z),
            BankTellerRules.CounterRange
        );
        var walk = tiles.Count > 0
            ? GoToSkill.FromPoints(tiles, WalkArrival.TileRouteEndRange(tiles[^1], banker.Location, BankTellerRules.CounterRange))
            : new GoToSkill(banker.Location, BankTellerRules.CounterRange);

        return walk.Begin(person) ? walk : null;
    }

    /// <summary>Says "bank". True when the banker opened the box.</summary>
    public static bool OpenBox(SosariaCharacter person)
    {
        if (person?.BankBox == null || FindBanker(person) == null)
        {
            return false;
        }

        Speak(person, BankTellerRules.BankLine, BankTellerRules.BankKeyword);
        return person.BankBox.Opened;
    }

    /// <summary>
    /// Drags pack gold above walking money into the opened box, as much as its piles and free
    /// slots take; the rest stays in the pack. Returns the gold moved.
    /// </summary>
    public static int DepositSurplus(SosariaCharacter person)
    {
        var pack = person?.Backpack;
        var amount = BankTellerRules.DepositAmount(pack?.GetAmount(typeof(Gold)) ?? 0);

        if (amount <= 0 || person.BankBox?.Opened != true || !pack.ConsumeTotal(typeof(Gold), amount))
        {
            return 0;
        }

        var deposited = Banker.DepositUpTo(person, amount);

        if (deposited < amount)
        {
            pack.DropItem(new Gold(amount - deposited));
        }

        return deposited;
    }

    /// <summary>
    /// True when <paramref name="box"/> takes gold as Banker.DepositUpTo does: onto a gold pile
    /// or a check with room, else into a free slot under its item cap. An all-or-nothing deposit
    /// into a full box put the gold back in the pack, and the heavy purse called the person to
    /// the counter again.
    /// </summary>
    public static bool BoxTakesGold(Container box)
    {
        if (box == null)
        {
            return false;
        }

        foreach (var held in box.Items)
        {
            if (held is Gold { Amount: < BankTellerRules.GoldPileCap } ||
                held is BankCheck { Worth: < BankTellerRules.CheckWorthCap })
            {
                return true;
            }
        }

        return box.MaxItems == BankTellerRules.NoItemCap || box.TotalItems < box.MaxItems;
    }

    /// <summary>
    /// Says "withdraw N" for the round amount that refills walking money. The banker moves the
    /// gold. Returns the gold that reached the pack.
    /// </summary>
    public static int WithdrawShortfall(SosariaCharacter person)
    {
        var pack = person?.Backpack;

        if (pack == null || FindBanker(person) == null)
        {
            return 0;
        }

        var before = pack.GetAmount(typeof(Gold));
        var amount = BankTellerRules.WithdrawAmount(
            before,
            Banker.GetBalance(person),
            BankTellerRules.WithdrawCeiling(Core.ML)
        );

        if (amount <= 0)
        {
            return 0;
        }

        Speak(person, BankTellerRules.WithdrawLine(amount), BankTellerRules.WithdrawKeyword);
        return pack.GetAmount(typeof(Gold)) - before;
    }

    /// <summary>Says "balance". The banker answers with the real figure.</summary>
    public static void AskBalance(SosariaCharacter person)
    {
        if (FindBanker(person) != null)
        {
            Speak(person, BankTellerRules.BalanceLine, BankTellerRules.BalanceKeyword);
        }
    }

    /// <summary>
    /// A visit to the counter: open the box, bank the surplus or draw the shortfall, and ask
    /// the balance now and then when there was nothing to move. Returns the gold moved in
    /// (positive) or out (negative).
    /// </summary>
    public static int SettleWalkingMoney(SosariaCharacter person)
    {
        if (!OpenBox(person))
        {
            return 0;
        }

        var deposited = DepositSurplus(person);

        if (deposited > 0)
        {
            return deposited;
        }

        var withdrawn = WithdrawShortfall(person);

        if (withdrawn == 0 && BankTellerRules.AsksBalance(Utility.Random(BankTellerRules.PercentScale)))
        {
            AskBalance(person);
        }

        return -withdrawn;
    }

    /// <summary>
    /// Asks the banker in earshot for <paramref name="amount"/> gold, up to one withdraw's ceiling,
    /// aloud as a player does. False when no banker hears it.
    /// </summary>
    public static bool Withdraw(SosariaCharacter person, int amount)
    {
        if (amount <= 0 || FindBanker(person) == null)
        {
            return false;
        }

        person.DoSpeech(
            BankTellerRules.WithdrawLine(Math.Min(amount, BankTellerRules.WithdrawCeiling(Core.ML))),
            [BankTellerRules.WithdrawKeyword],
            MessageType.Regular,
            person.SpeechHue
        );
        return true;
    }

    /// <summary>
    /// True when the purse is heavy enough to bank into a box that takes gold
    /// (<see cref="BoxTakesGold"/>), or light enough to refill.
    /// </summary>
    public static bool PurseNeedsBanker(SosariaCharacter person) =>
        person?.Backpack != null &&
        BankTellerRules.PurseNeedsBanker(
            person.Backpack.GetAmount(typeof(Gold)),
            Banker.GetBalance(person),
            BoxTakesGold(person.BankBox)
        );

    /// <summary>
    /// The gold coins in the pack and the bank box together: what <see cref="PayPackThenBank"/>
    /// can take. Bank checks are not counted (<see cref="VendorDeal.GoldHeld"/> counts them for a vendor).
    /// </summary>
    public static int CoinsHeld(Mobile person) =>
        (person?.Backpack?.GetAmount(typeof(Gold)) ?? 0) + (person?.BankBox?.GetAmount(typeof(Gold)) ?? 0);

    /// <summary>Pays <paramref name="amount"/> out of the pack first, then what the pack lacks out of the bank box.</summary>
    public static void PayPackThenBank(Mobile person, int amount)
    {
        var left = amount - (person?.Backpack?.ConsumeUpTo(typeof(Gold), amount) ?? 0);

        if (left > 0)
        {
            person?.BankBox?.ConsumeUpTo(typeof(Gold), left);
        }
    }

    private static void Speak(SosariaCharacter person, string line, int keyword)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} says at the bank: {Line}", person.Name, line);
        }

        person.DoSpeech(line, [keyword], MessageType.Regular, person.SpeechHue);
    }
}
