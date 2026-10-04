using System;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// A treasure map its holder cannot finish goes to the bank: the holder stands on the floor,
/// holds the map up with a WTS line and waits for a buyer, a character that can decode it or
/// a person who asks the price. Sold is done; nobody biting in the dwell is a failure, so the
/// next try waits.
/// </summary>
public sealed class TreasureMapSellSkill : Skill
{
    private static readonly ILogger logger = SosariaLog.For(typeof(TreasureMapSellSkill));

    private readonly Item _goods;
    private SosariaCharacter _character;
    private Skill _walk;
    private DateTime _heldSince;
    private DateTime _nextShout;
    private int _asking;

    public TreasureMapSellSkill(Item goods) => _goods = goods;

    public override string Name => CartographyRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _heldSince = default;

        if (!Carried())
        {
            return false;
        }

        _walk = new TravelSkill(BankTeller.BankToken, NavLimits.BankArrivalRange);
        return _walk.Begin(character);
    }

    public override SkillStatus Tick()
    {
        if (!CraftStationSkill.MayWork(_character))
        {
            return Finish(SkillStatus.Failed);
        }

        if (_walk != null)
        {
            var walk = _walk.Tick();

            if (walk == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _walk = null;
            return walk == SkillStatus.Done ? HoldUp() : Finish(SkillStatus.Failed);
        }

        if (!Carried())
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} sold a {Goods} at the bank", _character.Name, TreasureLines.NounOf(_goods));
            }

            return Finish(SkillStatus.Done);
        }

        var now = Core.Now;

        if (!TradeSessions.IsBusy(_character) &&
            TreasureMarketRules.DwellOver(now, _heldSince, TreasureMarketRules.SellDwell))
        {
            return Finish(SkillStatus.Failed);
        }

        if (now >= _nextShout)
        {
            Shout(now);
        }

        return SkillStatus.Running;
    }

    public override void Abort()
    {
        _walk?.Abort();
        Finish(SkillStatus.Failed);
    }

    public override void Resume(TimeSpan held)
    {
        _heldSince = SkillClock.Shift(_heldSince, held);
        _walk?.Resume(held);
    }

    private SkillStatus HoldUp()
    {
        if (!Carried())
        {
            return SkillStatus.Failed;
        }

        var now = Core.Now;
        _asking = TreasureMarket.Offer(_character, _goods, Utility.Random(int.MaxValue)).Asking;
        _heldSince = now;
        Shout(now);
        return SkillStatus.Running;
    }

    private void Shout(DateTime now)
    {
        var roll = Utility.Random(int.MaxValue);
        _nextShout = now + BankCrowdRules.ShoutGap(roll);
        Talk.Say(_character, TalkCategory.TreasureCannotRead, TreasureLines.Sale(_goods, _asking));
    }

    private bool Carried() => _goods is { Deleted: false } && _character != null && _goods.IsChildOf(_character.Backpack);

    private SkillStatus Finish(SkillStatus status)
    {
        TreasureMarket.Withdraw(_character, _goods);
        return status;
    }
}
