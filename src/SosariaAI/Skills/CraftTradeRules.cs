using System;
using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Economy;

namespace SosariaAI.Skills;

/// <summary>
/// One thing a crafter could make now: its place in the craft list, the engine's success
/// chance, whether the materials for one piece are in the pack or on a nearby shelf, the gold
/// a try earns past its materials (<see cref="CraftTradeRules.ExpectedProfit"/>), and whether
/// anyone in reach buys the piece at all.
/// </summary>
public readonly record struct CraftOption(int Index, double Chance, bool HasMaterials, int Profit, bool HasBuyer);

/// <summary>
/// How a crafter works a session at the shop: which item it makes, how many pieces go in a
/// batch, how long the session lasts, how much stock it lays in, and how many finished pieces
/// it keeps to hawk. A crafter makes goods someone buys: a counter in reach, or people who want
/// them, weighted by the gold a try earns, and a piece that still teaches weighs more. Most of
/// what even a grandmaster turned out was plain goods that sold. Carpenters sold nothing in over half their batches and
/// tailors sewed oil cloth no vendor buys while the pick chased the hardest item. A batch that
/// makes nothing twice drops its item for the session, and three in a row end it. When nothing
/// earns, it practises on the cheapest item that still teaches, as players ground skill on
/// daggers and lesser potions. A crafter stood at its station for most of an evening, working
/// at a person's pace. Pure. No world objects.
/// </summary>
public static class CraftTradeRules
{
    /// <summary>Below even odds a crafter burns more material than it turns into goods.</summary>
    public const double MinUsefulChance = 0.5;

    /// <summary>A chance at or above this no longer teaches anything.</summary>
    public const double CertainChance = 1.0;

    /// <summary>An item that still teaches weighs this many times its profit in the pick.</summary>
    public const int TeachWeight = 3;

    /// <summary>The least weight a pickable item carries, so a thin margin still has a turn.</summary>
    public const int MinPickWeight = 1;

    /// <summary>
    /// Goods no counter in reach takes but people want count at this share of the market table's
    /// value, in tenths: only the few pieces kept to hawk sell at the bank.
    /// </summary>
    public const int UnvendedShareTenths = 3;

    /// <summary>Empty batches in a row on one item before the crafter drops it for the session.</summary>
    public const int EmptyBatchesPerItem = 2;

    /// <summary>Empty batches in a row before the crafter ends the session and rests.</summary>
    public const int MaxEmptyBatches = 3;

    /// <summary>The engine's quality number for an exceptional piece, the one that asks for a maker's mark.</summary>
    public const int ExceptionalQuality = 2;

    private const int Tenths = 10;

    public const int MinBatchCrafts = 4;
    public const int MaxBatchCrafts = 10;
    public const int MinSessionMinutes = 30;
    public const int MaxSessionMinutes = 60;

    /// <summary>Pieces of stock a supply trip lays in: three full batches, so one trip feeds a long stretch at the station.</summary>
    public const int SupplyCrafts = MaxBatchCrafts * 3;

    /// <summary>
    /// Gold a crafter keeps back from stock so a broken tool can still be replaced, while it
    /// holds no spare (<see cref="ToolReserve"/>): the dearest tool of a T2A trade, a smith's
    /// hammer, costs 21 at the blacksmith and 23 at the tinker.
    /// </summary>
    public const int ToolReserveGold = 25;

    /// <summary>A tool count that says nothing of a spare: the reserve is kept.</summary>
    public const int NoToolsCounted = 0;

    /// <summary>The engine sells nothing for less than a coin.</summary>
    private const int MinUnitPrice = 1;

    /// <summary>Shops a crafter walks to for tool and stock before one stretch at the station.</summary>
    public const int MaxSupplyTrips = 2;

    /// <summary>Chance, out of a hundred, that a grandmaster calls out an exceptional piece.</summary>
    public const int MasterworkTalkPercent = 60;

    /// <summary>Finished pieces a crafter keeps back from the vendor to hawk at the bank.</summary>
    public const int HawkerStock = 2;

    /// <summary>Items the engine turns away in a row before the session ends.</summary>
    public const int MaxRejects = 3;

    /// <summary>
    /// Tools of its trade a crafter keeps: the one in use and a spare. A Trinsic tinker whose
    /// only tool broke found none for sale in reach and never worked again.
    /// </summary>
    public const int ToolsKept = 2;

    /// <summary>Pieces in a batch that makes the crafter's own spare tool.</summary>
    public const int SpareToolBatch = 1;

    /// <summary>
    /// How long a crafter out of stock holds its station, asking aloud and buying from any
    /// gatherer who comes by, before it gives the stretch up.
    /// </summary>
    public static readonly TimeSpan DryWait = TimeSpan.FromMinutes(3);

    /// <summary>How often a crafter waiting for stock looks for a gatherer or a restocked shelf.</summary>
    public static readonly TimeSpan DryCheckGap = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gold of loss past the smallest one that an item may carry and still be practised when
    /// nothing in reach earns: the cheap lessons. By the engine's sell tables no potion, scroll,
    /// meal or map and only six of 54 smith pieces pay back shelf stock, and smiths that took
    /// any loss-maker at even odds spent 1847 gold at the counter and sold for 978 in one evening.
    /// </summary>
    public const int PracticeLossSlack = 2;

    /// <summary>
    /// The least and the most seconds a crafter lets pass between two tries: it reads the
    /// result, clicks the menu again and looks up now and then, as a player at a forge did.
    /// A crafter works one cycle every six to fourteen seconds; the engine's own
    /// craft timer ends in about two, and a carpenter burnt its 62 logs in two minutes.
    /// </summary>
    public const int MinTryPauseSeconds = 3;

    public const int MaxTryPauseSeconds = 8;

    public const int NoPick = -1;

    private const int InclusiveSpanPad = 1;

    /// <summary>
    /// The position in <paramref name="options"/> to make, or <see cref="NoPick"/>. Only items with
    /// materials, even odds or better and a buyer count; when any of them earns gold, the ones
    /// that lose it drop out. When none earns, the crafter practises on the cheap lessons
    /// (<see cref="KeepCheapLessons"/>). The roll picks among the rest by weight: the gold a try
    /// earns, times <see cref="TeachWeight"/> for an item that still teaches.
    /// </summary>
    public static int Pick(IReadOnlyList<CraftOption> options, int roll)
    {
        var useful = new List<int>();
        var profitable = false;

        for (var i = 0; i < (options?.Count ?? 0); i++)
        {
            var option = options[i];

            if (option.HasMaterials && option.HasBuyer && option.Chance >= MinUsefulChance)
            {
                useful.Add(i);
                profitable |= option.Profit > 0;
            }
        }

        if (profitable)
        {
            useful.RemoveAll(i => options[i].Profit <= 0);
        }
        else
        {
            KeepCheapLessons(useful, options);
        }

        if (useful.Count == 0)
        {
            return NoPick;
        }

        var weights = new List<long>(useful.Count);
        long total = 0;

        for (var i = 0; i < useful.Count; i++)
        {
            var option = options[useful[i]];
            long weight = Math.Max(MinPickWeight, option.Profit) * (Teaches(option) ? TeachWeight : 1);
            weights.Add(weight);
            total += weight;
        }

        var point = Math.Abs((long)roll) % total;

        for (var i = 0; i < useful.Count; i++)
        {
            if (point < weights[i])
            {
                return useful[i];
            }

            point -= weights[i];
        }

        return useful[^1];
    }

    /// <summary>
    /// Narrows <paramref name="useful"/>, where no item earns gold, to what a player practised on:
    /// items that still teach when any does, and of those the ones within
    /// <see cref="PracticeLossSlack"/> of the smallest loss. A broke smith hammers daggers, not
    /// plate it cannot sell for its ingots.
    /// </summary>
    private static void KeepCheapLessons(List<int> useful, IReadOnlyList<CraftOption> options)
    {
        if (useful.Exists(i => Teaches(options[i])))
        {
            useful.RemoveAll(i => !Teaches(options[i]));
        }

        if (useful.Count == 0)
        {
            return;
        }

        var leastLoss = int.MinValue;

        for (var i = 0; i < useful.Count; i++)
        {
            leastLoss = Math.Max(leastLoss, options[useful[i]].Profit);
        }

        useful.RemoveAll(i => options[i].Profit < leastLoss - PracticeLossSlack);
    }

    private static bool Teaches(CraftOption option) => option.Chance < CertainChance;

    /// <summary>
    /// True when a try needs more of a body's pool (mana, stamina or hits) than the crafter has
    /// now but no more than it holds when full: it waits for the pool, as a scribe meditated
    /// between scrolls. The engine turns such a try away, and the station took each scroll turned
    /// away for one it cannot make: three in a row (<see cref="MaxRejects"/>) ended the session.
    /// </summary>
    public static bool WaitsToRecover(int have, int need, int max) => need > 0 && have < need && max >= need;

    /// <summary>The pause before the next try at the station, from <see cref="MinTryPauseSeconds"/> to <see cref="MaxTryPauseSeconds"/>.</summary>
    public static TimeSpan TryPause(int roll) =>
        TimeSpan.FromSeconds(MinTryPauseSeconds + Math.Abs(roll % (MaxTryPauseSeconds - MinTryPauseSeconds + InclusiveSpanPad)));

    /// <summary>The gold a try earns: the piece's worth at the engine's odds, less its materials.</summary>
    public static int ExpectedProfit(double chance, int worth, int materialCost) =>
        (int)Math.Round(Math.Clamp(chance, 0, CertainChance) * Math.Max(0, worth)) - Math.Max(0, materialCost);

    /// <summary>
    /// What a finished piece fetches: the best price a counter in reach pays, else a share of what
    /// people pay for it (<see cref="UnvendedShareTenths"/>), and never less than what shop stock
    /// adds (<paramref name="shopValue"/>, see <see cref="CraftShopRules.ShopShare"/>).
    /// </summary>
    public static int PieceWorth(int vendorPrice, int peopleValue, int shopValue = 0) =>
        Math.Max(
            Math.Max(0, shopValue),
            vendorPrice > 0 ? vendorPrice : Math.Max(0, peopleValue) * UnvendedShareTenths / Tenths
        );

    /// <summary>True when the item has made nothing in enough batches in a row to be dropped for the session.</summary>
    public static bool DropsItem(int emptyBatchesOnItem) => emptyBatchesOnItem >= EmptyBatchesPerItem;

    /// <summary>True when the session has made nothing in enough batches in a row to end and rest.</summary>
    public static bool RestsAfterEmpty(int emptyBatchesInRow) => emptyBatchesInRow >= MaxEmptyBatches;

    /// <summary>
    /// True when the engine asked the crafter whether to put its maker's mark on the piece and
    /// waits for the answer: the T2A menus ask a crafter at <see cref="SkillTierRules.GrandmasterSkill"/> for
    /// every exceptional piece of a markable item, and a person with no client never answered.
    /// A grandmaster smith lost 1027 daggers that way: the try ended, nothing came into the pack
    /// and not one ingot was burnt.
    /// </summary>
    public static bool AnswersMakersMark(bool menusAsk, double mainSkillBase, bool markable, bool madeNothing, bool materialsKept) =>
        menusAsk && mainSkillBase >= SkillTierRules.GrandmasterSkill && markable && madeNothing && materialsKept;

    public static int BatchSize(int roll) =>
        MinBatchCrafts + Math.Abs(roll % (MaxBatchCrafts - MinBatchCrafts + InclusiveSpanPad));

    public static TimeSpan SessionLength(int roll) =>
        TimeSpan.FromMinutes(
            MinSessionMinutes + Math.Abs(roll % (MaxSessionMinutes - MinSessionMinutes + InclusiveSpanPad))
        );

    /// <summary>Units of one material to buy so a batch of <paramref name="crafts"/> can run.</summary>
    public static int MaterialsToBuy(int perCraft, int crafts, int have) =>
        Math.Max(0, Math.Max(0, perCraft) * Math.Max(0, crafts) - Math.Max(0, have));

    /// <summary>How many whole pieces the carried material covers.</summary>
    public static int CraftsCovered(int perCraft, int have) =>
        perCraft <= 0 ? int.MaxValue : Math.Max(0, have) / perCraft;

    /// <summary>
    /// Gold held back for a new tool: <see cref="ToolReserveGold"/>, or nothing for a crafter that
    /// carries its spare (<see cref="ToolsKept"/>): the spare is the reserve. A mortar costs 8 and a
    /// sewing kit 3, and the 22 alchemists of one two-hour run brewed one potion.
    /// </summary>
    public static int ToolReserve(int toolsCarried) => toolsCarried >= ToolsKept ? 0 : ToolReserveGold;

    /// <summary>Gold a crafter may spend on stock: its purse less the reserve for a new tool (<see cref="ToolReserve"/>).</summary>
    public static int StockBudget(int gold, int toolsCarried = NoToolsCounted) => Math.Max(0, gold - ToolReserve(toolsCarried));

    /// <summary>
    /// True when the purse past the tool reserve pays for one unit at <paramref name="unitPrice"/>:
    /// a stocked shelf helps only then. A cook with a few coins walked nine minutes to a butcher
    /// and bought nothing there.
    /// </summary>
    public static bool PaysForUnit(int gold, int unitPrice, int toolsCarried = NoToolsCounted) =>
        StockBudget(gold, toolsCarried) >= Math.Max(MinUnitPrice, unitPrice);

    /// <summary>
    /// True when the purse pays for the tool at <paramref name="toolPrice"/>: the tool reserve is
    /// kept for this. A counter that shelves the tool is no help to a crafter who cannot pay: a
    /// scribe with four coins walked to the mage shop for a pen and failed "could not get a tool"
    /// every few minutes for an evening.
    /// </summary>
    public static bool PaysForTool(int gold, int toolPrice) => gold >= Math.Max(MinUnitPrice, toolPrice);

    /// <summary>True when a grandmaster made the piece exceptional: that one gets called out.</summary>
    public static bool AnnouncesMasterwork(double skill, bool exceptional) => exceptional && skill >= SkillTierRules.GrandmasterSkill;

    /// <summary>
    /// The places in <paramref name="values"/> of the finished pieces a crafter holds back from
    /// the shop to hawk at the bank: the <paramref name="room"/> worth the most, the first of
    /// equal pieces. The rest go over the counter.
    /// </summary>
    public static List<int> PiecesToKeep(IReadOnlyList<int> values, int room = HawkerStock) =>
        CraftShopRules.BestFirst(values, room);

    public static bool SessionOver(TimeSpan elapsed, TimeSpan length) => elapsed >= length;

    /// <summary>
    /// True when a counter answers what the crafter lacks: the tool when it lacks the tool,
    /// else the stock. The blacksmith's ingots are no answer to a tinker without tools.
    /// </summary>
    public static bool MeetsNeed(bool shelvesStock, bool sellsTool, bool needTool) => needTool ? sellsTool : shelvesStock;

    /// <summary>Tools to buy so the crafter holds its working tool and a spare.</summary>
    public static int ToolsToBuy(int carried) => Math.Max(0, ToolsKept - Math.Max(0, carried));

    /// <summary>
    /// True when the crafter makes its own spare before the batch: its trade makes its tool,
    /// and it holds one to make it with but no spare.
    /// </summary>
    public static bool MakesSpareTool(bool makesItsTool, int carried) => makesItsTool && carried > 0 && carried < ToolsKept;

    /// <summary>
    /// True when a crafter out of stock holds its station for a seller (<see cref="DryWait"/>):
    /// only for stock people bring, and not when it digs or cuts its own. A carpenter that
    /// chopped its own logs stood three minutes at its bench after every load, a fifth of its day.
    /// </summary>
    public static bool WaitsForSeller(bool peopleBringStock, bool gathersOwnStock) => peopleBringStock && !gathersOwnStock;

    public static bool DryWaitOver(TimeSpan waited) => waited >= DryWait;

    public static bool DryCheckDue(TimeSpan sinceCheck) => sinceCheck >= DryCheckGap;
}
