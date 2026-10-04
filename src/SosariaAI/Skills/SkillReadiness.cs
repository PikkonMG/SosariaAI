using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// The skills whose start needs something in the world that is not there: room for a pet
/// and a beast to tame in sight or on a taming ground in reach, a hurt beast and a bandage to
/// tend it, a beast to study, a creature to discord or two to provoke, a fight to calm, an instrument to play,
/// someone to beg from, heat to cook at and a pan and meat to cook with, someone hidden to
/// search for, a trapped chest to disarm, a lockpick and a locked chest to pick, a poison
/// potion and a blade or food to coat, a taster's training to taste food, the axe, pick
/// or pole a harvest needs and a site in reach not found bare, and for a crafter a free place
/// to work, the tool of its trade and its
/// stock, carried, on a shelf in reach at a price it can pay or held up at the bank; for a
/// cartographer map work or the makings of a map. A crafter's own harvest waits
/// until it runs low and cannot buy any stock. Each test is
/// the one the skill itself runs, so the scorer drops a job
/// that would fail at its start instead of picking it, failing, and picking it again at
/// the Britain bank.
/// </summary>
public static class SkillReadiness
{
    /// <summary>The skill kinds with a world precondition this class tests.</summary>
    public static readonly string[] GatedKinds =
    [
        SkillKinds.Tame, SkillKinds.Vet, SkillKinds.Lore, SkillKinds.Discord, SkillKinds.Peace, SkillKinds.Provoke, SkillKinds.Beg, SkillKinds.Cook,
        SkillKinds.DetectHidden, SkillKinds.RemoveTrap, SkillKinds.Lockpick, SkillKinds.Poison, SkillKinds.Taste,
        SkillKinds.Mine, SkillKinds.Lumberjack, SkillKinds.Fish, SkillKinds.Smith, SkillKinds.Tailor, SkillKinds.Carpentry,
        SkillKinds.Fletch, SkillKinds.Alchemy, SkillKinds.Inscription, SkillKinds.Tinker, SkillKinds.Cartography
    ];

    /// <summary>The gated skills among the character's routines whose precondition fails where it stands.</summary>
    public static IReadOnlyList<string> Unmet(SosariaCharacter character)
    {
        var unmet = new List<string>();

        if (character?.Definition == null || !People.InWorld(character))
        {
            return unmet;
        }

        for (var i = 0; i < GatedKinds.Length; i++)
        {
            var kind = GatedKinds[i];

            if (character.Definition.UsesSkill(kind) && !IsReady(character, kind))
            {
                unmet.Add(kind);
            }
        }

        RedGangReach.AddUnreachableOutings(character, unmet);
        return unmet;
    }

    private static bool IsReady(SosariaCharacter character, string kind) =>
        kind switch
        {
            SkillKinds.Tame => TameSkill.CanSeek(character),
            SkillKinds.Vet => VetSkill.HasWork(character),
            SkillKinds.Lore => LoreSkill.FindSubject(character) != null,
            SkillKinds.Discord => MusicRules.FindInstrument(character) != null && DiscordSkill.FindTarget(character) != null,
            SkillKinds.Peace => MusicRules.FindInstrument(character) != null && PeaceSkill.HasFight(character),
            SkillKinds.Provoke => MusicRules.FindInstrument(character) != null && ProvokeSkill.FindPair(character, out _, out _),
            SkillKinds.Beg => BegSkill.FindTarget(character) != null,
            SkillKinds.Cook => CookRules.CanCook(character),
            SkillKinds.DetectHidden => DetectHiddenSkill.HasWork(character),
            SkillKinds.RemoveTrap => RemoveTrapSkill.HasWork(character),
            SkillKinds.Lockpick => LockpickSkill.HasWork(character),
            SkillKinds.Poison => PoisonRules.HasWork(character),
            SkillKinds.Taste => TasteRules.MayBegin(character),
            SkillKinds.Mine or SkillKinds.Lumberjack or SkillKinds.Fish =>
                WorkerTools.HasToolFor(character, kind) && !StocksElsewhere(character, kind) &&
                !HarvestPatches.IsDry(character.Serial.Value, kind, Core.Now),
            SkillKinds.Cartography => CartographySkill.HasMapWork(character) || CanCraft(character, CartographyRules.Trade),
            _ when CraftCareerRules.TradeByKind(kind) is { } trade => CanCraft(character, trade),
            _ => true
        };

    /// <summary>
    /// True when <paramref name="kind"/> is the harvest of the character's own trade and it has
    /// no need to work it: a smith digs ore only once its ingots run low and no gatherer at the
    /// bank in reach sells it any it can afford (<see cref="CraftMarketRules.GoesForOwnStock"/>).
    /// </summary>
    private static bool StocksElsewhere(SosariaCharacter character, string kind)
    {
        if (CraftMarket.TradeOf(character) is not { } trade || trade.GatherKind != kind)
        {
            return false;
        }

        // The bank search runs only for a crafter that is low.
        var carried = CraftStations.StockCarried(character, trade);
        return carried >= CraftMarketRules.OwnStockLow ||
               !CraftMarketRules.GoesForOwnStock(carried, BankStock.OfferedInReach(character, trade));
    }

    /// <summary>
    /// A station trade starts only with its own ore smelted, a place to work that is not full, a
    /// tool, and the makings of a piece carried or on a shelf the purse pays for: smiths failed "no Smith shop in reach"
    /// 38 times in half an hour, most at the Skara Brae bank, and tailors stood out of cloth at
    /// sold-out shelves.
    /// </summary>
    private static bool CanCraft(SosariaCharacter character, CraftTrade trade) =>
        !CraftStations.SmeltsFirst(character, trade) &&
        CraftStations.ShopFor(character, trade) != null &&
        CraftStations.CanGetTool(character, trade) &&
        CraftStations.CanGetStock(character, trade);
}
