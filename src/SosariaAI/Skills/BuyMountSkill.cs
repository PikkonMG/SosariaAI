using System;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Buy a horse from an animal trainer through the real vendor path. The purchase
/// spawns the mount already controlled; the copy only has to stand close and pay. The
/// trainer takes a horse's price from the pack only (<see cref="PackFunds"/>), so a buyer
/// whose pack is short and whose bank holds the rest goes by the bank first and says
/// "withdraw", as a player did.
/// </summary>
public sealed class BuyMountSkill : Skill
{
    private const string ShortWhy = "it cannot pay for a horse";
    private const string NoTrainerWhy = "no animal trainer in reach";
    private const string NoBankWalkWhy = "no walk to the bank";
    private const string NoStableWalkWhy = "no walk to the stable";
    private const string WithdrawWhy = "the banker gave it no gold";
    private const string TrainerGoneWhy = "the trainer was not at the stable";
    private const string SoldOutWhy = "the trainer has no horse to sell";
    private const string RefusedWhy = "the trainer would not sell";

    private SosariaCharacter _character;
    private Skill _walk;
    private Point3D _stable;
    private bool _toBank;

    public override string Name => SkillKinds.BuyMount;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;

        if (!People.InWorld(character))
        {
            return CannotStart(ShortWhy);
        }

        if (!MountBuyRules.MayBuy(
                character.Mounted,
                OwnsMountNearby(),
                character.Followers,
                MountBuyRules.HorseSlots,
                character.FollowersMax))
        {
            return CannotStart("it has a mount or no room for one");
        }

        var pack = PackGold();

        if (!MountBuyRules.CanFund(pack, Banker.GetBalance(character), PkRules.IsRed(character.Kills)))
        {
            return CannotStart(ShortWhy);
        }

        _stable = ShopFinder.Nearest(character, ShopFinder.AnimalTrainerToken, Point3D.Zero);

        if (_stable == Point3D.Zero)
        {
            return CannotStart(NoTrainerWhy);
        }

        _toBank = !MountBuyRules.CanAfford(pack);
        _walk = _toBank
            ? new TravelSkill(BankTeller.BankToken, NavLimits.BankArrivalRange)
            : new TravelSkill(_stable, NavLimits.ShopArrivalRange, arrivalFloor: true);
        return _walk.Begin(character) || CannotStart(_walk.FailReason ?? (_toBank ? NoBankWalkWhy : NoStableWalkWhy));
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || !People.InWorld(_character))
        {
            return Fail("it left the world");
        }

        var walk = _walk?.Tick() ?? SkillStatus.Failed;

        if (walk == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        if (walk == SkillStatus.Failed)
        {
            return Fail(_walk?.FailReason ?? (_toBank ? NoBankWalkWhy : NoStableWalkWhy));
        }

        _walk = null;
        return _toBank ? AtBank() : BuyHorse();
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }

    public override void Resume(TimeSpan held) => _walk?.Resume(held);

    private int PackGold() => _character.Backpack?.GetAmount(typeof(Gold)) ?? 0;

    // At the bank: one "withdraw" for what the pack lacks, then the walk to the stable.
    private SkillStatus AtBank()
    {
        var before = PackGold();

        if (!BankTeller.Withdraw(_character, MountBuyRules.HorsePrice - before) || PackGold() <= before)
        {
            return Fail(WithdrawWhy);
        }

        _toBank = false;
        _walk = new TravelSkill(_stable, NavLimits.ShopArrivalRange, arrivalFloor: true);
        return _walk.Begin(_character) ? SkillStatus.Running : Fail(_walk.FailReason ?? NoStableWalkWhy);
    }

    private bool OwnsMountNearby()
    {
        foreach (var mobile in _character.GetMobilesInRange(MountRules.SearchTiles))
        {
            if (mobile is BaseMount { Deleted: false, IsDeadPet: false } mount &&
                MountRules.RiderControls(
                    mount.Controlled,
                    mount.ControlMaster == _character,
                    mount.Summoned,
                    mount.SummonMaster == _character))
            {
                return true;
            }
        }

        return false;
    }

    private SkillStatus BuyHorse()
    {
        if (FindTrainer() is not IVendor trader || trader is not BaseVendor vendor)
        {
            return Fail(TrainerGoneWhy);
        }

        VendorDeal.RestockIfDue(vendor);

        foreach (var info in vendor.GetBuyInfo() ?? [])
        {
            if (info is not GenericBuyInfo buy || buy.Type != typeof(Horse))
            {
                continue;
            }

            if (buy.GetDisplayEntity() is not { } entity || !PackFunds.FundPack(_character, Math.Max(1, buy.Price)))
            {
                return Fail(ShortWhy);
            }

            if (!trader.OnBuyItems(_character, [new BuyItemResponse(entity.Serial, 1)]))
            {
                return Fail(RefusedWhy);
            }

            FollowOwnedMounts();

            // The goal loop scores again as soon as a step ends, so the Mount step that
            // follows in the routine never ran. The horse spawns on the buyer's own
            // tile, so this is the one moment it is certain to be in reach.
            OwnedMounts.ClimbNearest(_character);
            return SkillStatus.Done;
        }

        return Fail(SoldOutWhy);
    }

    /// <summary>The vendor leaves a bought mount on Stop; a mount the owner can lose is a pet that Follows.</summary>
    private void FollowOwnedMounts()
    {
        foreach (var mobile in _character.GetMobilesInRange(MountRules.SearchTiles))
        {
            if (mobile is BaseCreature { Deleted: false } creature &&
                MountRules.RiderControls(
                    creature.Controlled,
                    creature.ControlMaster == _character,
                    creature.Summoned,
                    creature.SummonMaster == _character))
            {
                creature.ControlTarget = _character;
                creature.ControlOrder = OrderType.Follow;
            }
        }
    }

    private BaseVendor FindTrainer()
    {
        foreach (var mobile in _character.Map.GetMobilesInRange(_character.Location, MountBuyRules.VendorSearchRange))
        {
            if (mobile is AnimalTrainer { Deleted: false } trainer)
            {
                return trainer;
            }
        }

        return null;
    }
}
