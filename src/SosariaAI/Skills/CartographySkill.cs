using System;
using Server;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// A cartographer's work step. With a treasure map it can finish, it hunts the treasure; with
/// one it cannot, it sells the map at the bank. Without a map it looks for one held up for sale
/// when it has heard of one or has coin to spend, and buys and hunts it; otherwise it draws
/// maps at the mapmaker. A hunt cut short goes back to its dug chest first.
/// </summary>
public sealed class CartographySkill : Skill
{
    private SosariaCharacter _character;
    private Skill _work;

    public override string Name => CartographyRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _work = null;

        if (!CraftStationSkill.MayWork(character))
        {
            return false;
        }

        _work = Choose(character);
        return _work.Begin(character);
    }

    public override SkillStatus Tick()
    {
        if (_work == null)
        {
            return SkillStatus.Failed;
        }

        var status = _work.Tick();

        if (status != SkillStatus.Done || _work is not TreasureMapBuySkill { Bought: { } map })
        {
            return status;
        }

        // A map just bought is hunted at once.
        _work = new TreasureHuntSkill(map);
        return _work.Begin(_character) ? SkillStatus.Running : SkillStatus.Done;
    }

    public override void Abort()
    {
        _work?.Abort();
        _work = null;
    }

    public override void Resume(TimeSpan held) => _work?.Resume(held);

    /// <summary>
    /// True when the cartographer has map work that needs no blank map: a dug chest to go back
    /// to, a treasure map to hunt or sell, or one held up for sale nearby.
    /// </summary>
    public static bool HasMapWork(SosariaCharacter character) =>
        character?.Backpack != null &&
        (TreasureMaps.UnlootedDig(character) != null || TreasureMaps.FirstOpen(character.Backpack) != null ||
         TreasureMarket.FindFor(character, TreasureMarketRules.NoticeRange) != null);

    private static Skill Choose(SosariaCharacter character)
    {
        if (TreasureMaps.UnlootedDig(character) is { } dug)
        {
            return new TreasureHuntSkill(dug);
        }

        var map = TreasureMaps.FirstOpen(character.Backpack);
        var task = TreasureHuntRules.Choose(
            map != null,
            map != null && TreasureMaps.MayHunt(character, map),
            map == null && TreasureMarket.FindFor(character, TreasureMarketRules.NoticeRange) != null,
            character.Backpack?.GetAmount(typeof(Gold)) ?? 0,
            Utility.Random(PercentRoll.Scale)
        );

        return task switch
        {
            CartographyTask.Hunt => new TreasureHuntSkill(map),
            CartographyTask.Sell => new TreasureMapSellSkill(map),
            CartographyTask.Shop => new TreasureMapBuySkill(),
            _ => new CartographyCraftSkill()
        };
    }
}
