using System;
using SosariaAI.Configuration;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// Measured world facts used to prove a step. A Done skill status is not proof.
/// </summary>
public readonly record struct WorldFacts(
    int Gold,
    int BankGold,
    int Goods,
    int Tools,
    bool ArmorEquipped,
    bool ItemObtained,
    string ObtainedItem,
    string Place,
    bool AtHome,
    bool InParty,
    string PartyPartner,
    bool Alive,
    bool Fought,
    int HuntKills,
    int Hits,
    bool CorpseRecovered
);

public readonly record struct StepObservation(
    string SkillKind,
    StepResultKind Kind,
    string Detail,
    WorldFacts Before,
    WorldFacts After
);

public static class StepProof
{
    public const string SuccessEquippedArmor = "equipped-armor";
    public const string SuccessGoldEarned = "gold-earned";
    public const string SuccessItemObtained = "item-obtained";
    public const string SuccessPartyJoined = "party-joined";
    public const string SuccessHuntReturned = "hunt-returned";
    public const string SuccessHuntedTogether = "hunted-together";
    public const string SuccessCorpseRecovered = "corpse-recovered";

    public const string DetailGoodsGained = "goods-gained";
    public const string DetailSoldGoods = "sold-goods";
    public const string DetailBoughtItem = "bought-item";
    public const string DetailEquippedArmor = "equipped-armor";
    public const string DetailFought = "fought";
    public const string DetailPartyJoined = "party-joined";
    public const string DetailBanked = "banked";
    public const string DetailWentHome = "went-home";
    public const string DetailHealed = "healed";
    public const string DetailCorpseRecovered = "corpse-recovered";
    public const string DetailNoWorldChange = "no-world-change";
    public const string DetailUnavailable = "unavailable";
    public const string DetailInterrupted = "interrupted";
    public const string DetailAttemptFailed = "attempt-failed";
    public const string DetailDead = "dead";

    public static StepObservation Observe(
        string skillKind,
        WorldFacts before,
        WorldFacts after,
        bool skillDone,
        bool interrupted,
        bool unavailable
    )
    {
        if (unavailable)
        {
            return new StepObservation(skillKind, StepResultKind.ActionUnavailable, DetailUnavailable, before, after);
        }

        if (!after.Alive)
        {
            return new StepObservation(skillKind, StepResultKind.Interrupted, DetailDead, before, after);
        }

        if (interrupted)
        {
            return new StepObservation(skillKind, StepResultKind.Interrupted, DetailInterrupted, before, after);
        }

        if (!skillDone)
        {
            return new StepObservation(skillKind, StepResultKind.AttemptFailed, DetailAttemptFailed, before, after);
        }

        var (ok, detail) = Proved(skillKind, before, after);

        if (!ok)
        {
            return new StepObservation(skillKind, StepResultKind.NoUsefulProgress, DetailNoWorldChange, before, after);
        }

        return new StepObservation(skillKind, StepResultKind.StepCompleted, detail, before, after);
    }

    public static bool MeetsSuccess(string success, WorldFacts facts)
    {
        if (string.IsNullOrWhiteSpace(success) || !facts.Alive)
        {
            return false;
        }

        return success.Trim() switch
        {
            SuccessEquippedArmor => facts.ArmorEquipped,
            SuccessGoldEarned => facts.Gold + facts.BankGold > 0,
            SuccessItemObtained => facts.ItemObtained || facts.Tools > 0 || facts.ArmorEquipped,
            SuccessPartyJoined => facts.InParty && !string.IsNullOrWhiteSpace(facts.PartyPartner),
            SuccessHuntReturned => facts.AtHome && facts.HuntKills > 0,
            SuccessHuntedTogether => facts.InParty && facts.Fought,
            SuccessCorpseRecovered => facts.CorpseRecovered,
            _ => false
        };
    }

    public static (bool Ok, string Detail) Proved(string skillKind, WorldFacts before, WorldFacts after)
    {
        if (string.IsNullOrWhiteSpace(skillKind))
        {
            return (false, DetailNoWorldChange);
        }

        if (IsHarvest(skillKind))
        {
            return after.Goods > before.Goods ? (true, DetailGoodsGained) : (false, DetailNoWorldChange);
        }

        if (Is(skillKind, SkillKinds.VendorSell))
        {
            var sold = after.Gold > before.Gold && after.Goods < before.Goods;
            return sold ? (true, DetailSoldGoods) : (false, DetailNoWorldChange);
        }

        if (Is(skillKind, SkillKinds.VendorBuy) ||
            Is(skillKind, SkillKinds.UpgradeGear) ||
            Is(skillKind, SkillKinds.Smith))
        {
            var spent = after.Gold + after.BankGold < before.Gold + before.BankGold;
            var gained = after.ItemObtained || after.Tools > before.Tools ||
                         after.ArmorEquipped && !before.ArmorEquipped;

            if (Is(skillKind, SkillKinds.UpgradeGear) &&
                after.ArmorEquipped && !before.ArmorEquipped)
            {
                return (true, DetailEquippedArmor);
            }

            return spent && gained ? (true, DetailBoughtItem) : (false, DetailNoWorldChange);
        }

        if (Is(skillKind, SkillKinds.Hunt) ||
            Is(skillKind, SkillKinds.Dungeon))
        {
            var hunted = after.Fought || after.HuntKills > before.HuntKills;
            return hunted ? (true, DetailFought) : (false, DetailNoWorldChange);
        }

        if (Is(skillKind, SkillKinds.Follow) ||
            Is(skillKind, SkillKinds.Visit))
        {
            var joined = after.InParty && !string.IsNullOrWhiteSpace(after.PartyPartner);
            return joined ? (true, DetailPartyJoined) : (false, DetailNoWorldChange);
        }

        if (Is(skillKind, SkillKinds.BankDeposit))
        {
            var banked = after.BankGold > before.BankGold || after.Goods < before.Goods;
            return banked ? (true, DetailBanked) : (false, DetailNoWorldChange);
        }

        if (Is(skillKind, SkillKinds.GoHome) ||
            Is(skillKind, SkillKinds.Recall))
        {
            return after.AtHome && !before.AtHome ? (true, DetailWentHome) : (false, DetailNoWorldChange);
        }

        if (Is(skillKind, SkillKinds.Heal) ||
            Is(skillKind, SkillKinds.Rest))
        {
            return after.Hits > before.Hits ? (true, DetailHealed) : (false, DetailNoWorldChange);
        }

        if (Is(skillKind, GhostSkill.SkillName))
        {
            return after.Alive && after.CorpseRecovered ? (true, DetailCorpseRecovered) : (false, DetailNoWorldChange);
        }

        if (!string.Equals(after.Place, before.Place, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(after.Place))
        {
            return (true, after.Place);
        }

        return (false, DetailNoWorldChange);
    }

    private static bool Is(string skillKind, string kind) => skillKind.Equals(kind, StringComparison.OrdinalIgnoreCase);

    private static bool IsHarvest(string skillKind) =>
        Is(skillKind, SkillKinds.Mine) ||
        Is(skillKind, SkillKinds.Lumberjack) ||
        Is(skillKind, SkillKinds.Fish);
}
