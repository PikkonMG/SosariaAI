using System;

namespace SosariaAI.Economy;

/// <summary>What a person at the bank does next about the counter.</summary>
public enum CounterTurn
{
    /// <summary>The banker hears an honest person: say "bank".</summary>
    Open,

    /// <summary>The banker does no business with a criminal.</summary>
    Criminal,

    /// <summary>No banker hears yet: step up to the counter, or to a free tile at it.</summary>
    StepCloser,

    /// <summary>No banker works on this bank floor.</summary>
    NoBanker,

    /// <summary>The steps to the counter are used up and still no banker hears.</summary>
    OutOfHearing
}

/// <summary>
/// Banking said aloud at a banker, the way a player types it: "bank" opens the box,
/// "withdraw 2000" moves two thousand gold, "balance" asks what the account holds. The
/// banker's own keywords make the words work, so an amount is only named when the account
/// really holds it. A person keeps walking money in the pack, banks the surplus above it,
/// and draws more when the purse runs low. Pure. No world objects.
/// </summary>
public static class BankTellerRules
{
    /// <summary>Banker keyword for "withdraw" (Banker.OnSpeech).</summary>
    public const int WithdrawKeyword = 0x0000;

    /// <summary>Banker keyword for "balance".</summary>
    public const int BalanceKeyword = 0x0001;

    /// <summary>Banker keyword for "bank".</summary>
    public const int BankKeyword = 0x0002;

    public const string BankLine = "bank";
    public const string BalanceLine = "balance";
    public const string WithdrawWord = "withdraw";

    /// <summary>The banker hears within this many tiles (Banker.HandlesOnSpeech).</summary>
    public const int BankerHearing = 12;

    /// <summary>How far across the bank floor a person looks for a banker to walk to.</summary>
    public const int BankerSearchRange = 16;

    /// <summary>
    /// How far from where a bank trip ended a person looks for the banker to walk to. The
    /// trip ends up to the bank's quiet range (16) from its spot, a copy's spot is scattered
    /// up to eight tiles off the town's bank marker, and Magincia's marker stands twelve
    /// tiles from its banker: walkers there found no banker within sixteen tiles and failed
    /// at the box.
    /// </summary>
    public const int BankerWalkRange = 40;

    /// <summary>A person steps up to within this many tiles of the banker.</summary>
    public const int CounterRange = 3;

    /// <summary>
    /// Walks to the counter before the person gives the visit up: the first, and one more
    /// after a short wait when the banker stepped off or the counter was crowded.
    /// </summary>
    public const int MaxCounterSteps = 2;

    /// <summary>Gold a person keeps in the pack for shops, stables and deals.</summary>
    public const int WalkingMoney = 1000;

    /// <summary>A pack below this share of walking money has a reason to visit the banker.</summary>
    public const double LowPurseShare = 0.25;

    /// <summary>The gold a pack keeps when it fills the rebuy fund: the low purse line.</summary>
    public const int PocketMoney = (int)(WalkingMoney * LowPurseShare);

    /// <summary>No rebuy fund kept in the box.</summary>
    public const int NoFund = 0;

    /// <summary>A pack above this multiple of walking money has a reason to bank the surplus.</summary>
    public const int HeavyPurseMultiple = 3;

    /// <summary>Amounts below this are not worth crossing the room for.</summary>
    public const int MinTransaction = 100;

    /// <summary>People type round numbers.</summary>
    public const int RoundTo = 100;

    /// <summary>"Thou canst not withdraw so much at one time!" before Mondain's Legacy.</summary>
    public const int ClassicWithdrawCeiling = 5000;

    /// <summary>The ceiling from Mondain's Legacy on.</summary>
    public const int ModernWithdrawCeiling = 60000;

    /// <summary>Out of a hundred visits with nothing to move, how many ask the balance anyway.</summary>
    public const int BalanceAskPercent = 35;

    public const int PercentScale = 100;

    /// <summary>A container item cap of zero holds any number of items (Container.CheckHold).</summary>
    public const int NoItemCap = 0;

    /// <summary>The most gold one pile holds (Banker.Deposit).</summary>
    public const int GoldPileCap = 60000;

    /// <summary>The most a bank check is worth (Banker.Deposit).</summary>
    public const int CheckWorthCap = 1000000;

    public static int WithdrawCeiling(bool mondainsLegacy) =>
        mondainsLegacy ? ModernWithdrawCeiling : ClassicWithdrawCeiling;

    /// <summary>
    /// The rebuy fund an armed person keeps in its box (<see cref="GearPlan.RebuyFund"/>); one
    /// without its arms keeps none and draws it to dress.
    /// </summary>
    public static int FundKept(bool armed, int rebuyFund) => armed ? rebuyFund : NoFund;

    /// <summary>
    /// Gold that goes into the box, or 0 when too little to bother: the surplus above walking
    /// money, and first what the rebuy <paramref name="fund"/> lacks, down to
    /// <see cref="PocketMoney"/>. Only gold above walking money went in, so a person with less
    /// carried it all, lost it with its body, and found its box empty.
    /// </summary>
    public static int DepositAmount(int packGold, int balance, int fund)
    {
        var amount = Math.Max(packGold - WalkingMoney, FundTopUp(packGold, balance, fund));
        return amount >= MinTransaction ? amount : 0;
    }

    /// <summary>
    /// The round amount to draw to refill walking money: never more than the account holds
    /// above the rebuy <paramref name="fund"/> or the banker allows at once, and 0 when the
    /// pack already carries enough.
    /// </summary>
    public static int WithdrawAmount(int packGold, int balance, int ceiling, int fund)
    {
        var need = WalkingMoney - Math.Max(0, packGold);
        var spare = balance - Math.Max(0, fund);

        if (need < MinTransaction || spare < MinTransaction || ceiling < MinTransaction)
        {
            return 0;
        }

        var wanted = RoundUp(need);
        var available = RoundDown(Math.Min(spare, ceiling));
        return Math.Min(wanted, available);
    }

    public static string WithdrawLine(int amount) => $"{WithdrawWord} {amount}";

    /// <summary>
    /// A purse worth a trip to the banker: too heavy to carry, or able to fill the rebuy
    /// <paramref name="fund"/>, while the box takes gold (<paramref name="boxTakesGold"/>); or
    /// too light to shop over an account that holds more than the fund. A box that refuses the
    /// gold is no reason for a trip: the person keeps its gold in the pack.
    /// </summary>
    public static bool PurseNeedsBanker(int packGold, int balance, bool boxTakesGold, int fund) =>
        boxTakesGold && (packGold > WalkingMoney * HeavyPurseMultiple || FundTopUp(packGold, balance, fund) >= MinTransaction) ||
        packGold < PocketMoney && balance - Math.Max(0, fund) >= MinTransaction;

    /// <summary>
    /// The next move at the bank. The box opens only where the banker hears, and never for a
    /// criminal: bank trips said "bank" twenty tiles from the Britain and Magincia bankers
    /// and failed "the banker would not open the box" 51 times in half an hour.
    /// </summary>
    public static CounterTurn AtCounter(bool criminal, bool bankerHears, bool bankerOnFloor, int steps)
    {
        if (criminal)
        {
            return CounterTurn.Criminal;
        }

        if (bankerHears)
        {
            return CounterTurn.Open;
        }

        if (!bankerOnFloor)
        {
            return CounterTurn.NoBanker;
        }

        return steps < MaxCounterSteps ? CounterTurn.StepCloser : CounterTurn.OutOfHearing;
    }

    public static bool AsksBalance(int roll) => Math.Abs(roll % PercentScale) < BalanceAskPercent;

    // What the fund lacks, as far as the pack holds it above its pocket money; negative when the pack is below that.
    private static int FundTopUp(int packGold, int balance, int fund) =>
        Math.Min(Math.Max(0, fund - Math.Max(0, balance)), packGold - PocketMoney);

    private static int RoundUp(int amount) => (amount + RoundTo - 1) / RoundTo * RoundTo;

    private static int RoundDown(int amount) => amount / RoundTo * RoundTo;
}
