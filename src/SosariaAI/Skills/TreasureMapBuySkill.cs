using System;
using Server;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// A cartographer without a map looks for one held up for sale: a fisherman on the pier or a
/// hunter at the bank it heard of, else whatever the bank floor has. It walks over to a map it
/// can finish and pay for and haggles out loud (<see cref="DealVisit"/>); the gold and
/// the map change hands for real. <see cref="Bought"/> names the map after a deal.
/// </summary>
public sealed class TreasureMapBuySkill : Skill
{
    private enum Phase
    {
        WalkBank,
        Browse,
        Visit
    }

    private SosariaCharacter _character;
    private Skill _walk;
    private Phase _phase;
    private MapOffer _offer;
    private DealVisit _visit;
    private DateTime _browseSince;
    private DateTime _nextLook;

    public override string Name => CartographyRules.Kind;

    /// <summary>The map this trip bought, or null.</summary>
    public TreasureMap Bought { get; private set; }

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        Bought = null;
        _visit = null;
        _browseSince = default;

        if (TreasureMarket.FindFor(character, TreasureMarketRules.NoticeRange) is { } heard)
        {
            return StartApproach(heard);
        }

        _phase = Phase.WalkBank;
        _walk = new TravelSkill(BankTeller.BankToken, NavLimits.BankArrivalRange);
        return _walk.Begin(character);
    }

    public override SkillStatus Tick()
    {
        if (!CraftStationSkill.MayWork(_character))
        {
            return Finish(SkillStatus.Failed);
        }

        return _phase switch
        {
            Phase.WalkBank => TickWalkBank(),
            Phase.Browse => TickBrowse(),
            _ => TickVisit()
        };
    }

    public override void Abort()
    {
        _walk?.Abort();
        Finish(SkillStatus.Failed);
    }

    public override void Resume(TimeSpan held)
    {
        _browseSince = SkillClock.Shift(_browseSince, held);
        _walk?.Resume(held);
        _visit?.Resume(held);
    }

    private SkillStatus TickWalkBank()
    {
        var walk = _walk.Tick();

        if (walk == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _walk = null;

        if (walk != SkillStatus.Done)
        {
            return SkillStatus.Failed;
        }

        _phase = Phase.Browse;
        _browseSince = Core.Now;
        return SkillStatus.Running;
    }

    private SkillStatus TickBrowse()
    {
        var now = Core.Now;

        if (now < _nextLook)
        {
            return SkillStatus.Running;
        }

        _nextLook = now + TreasureMarketRules.BrowseGap;

        if (TreasureMarket.FindFor(_character, TreasureMarketRules.NoticeRange) is { } offer)
        {
            return StartApproach(offer) ? SkillStatus.Running : SkillStatus.Failed;
        }

        return TreasureMarketRules.DwellOver(now, _browseSince, TreasureMarketRules.BrowseDwell)
            ? SkillStatus.Failed
            : SkillStatus.Running;
    }

    private bool StartApproach(MapOffer offer)
    {
        _offer = offer;
        _phase = Phase.Visit;
        _visit = DealVisit.Start(_character, offer.Seller, offer.Map, offer.Asking);
        return _visit != null;
    }

    private SkillStatus TickVisit()
    {
        var status = _visit.Tick(Core.Now);

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _visit = null;

        if (status != SkillStatus.Done || !_offer.Map.IsChildOf(_character.Backpack))
        {
            return SkillStatus.Failed;
        }

        Bought = _offer.Map;
        return SkillStatus.Done;
    }

    private SkillStatus Finish(SkillStatus status)
    {
        _visit?.Abort();
        _visit = null;
        return status;
    }
}
