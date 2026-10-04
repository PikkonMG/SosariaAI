using System;
using Server;
using Server.Mobiles;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Buy a horse from an animal trainer through the real vendor path. The purchase
/// spawns the mount already controlled; the copy only has to stand close and pay.
/// </summary>
public sealed class BuyMountSkill : Skill
{
    private SosariaCharacter _character;
    private TravelSkill _walk;
    private bool _buying;

    public override string Name => SkillKinds.BuyMount;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _buying = false;

        if (!People.InWorld(character))
        {
            return false;
        }

        if (!MountBuyRules.MayBuy(
                character.Mounted,
                OwnsMountNearby(),
                character.Followers,
                MountBuyRules.HorseSlots,
                character.FollowersMax))
        {
            return false;
        }

        var stable = ShopFinder.Nearest(character, ShopFinder.AnimalTrainerToken, Point3D.Zero);

        if (stable == Point3D.Zero)
        {
            return false;
        }

        _walk = new TravelSkill(stable, NavLimits.ShopArrivalRange, arrivalFloor: true);
        return _walk.Begin(character);
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || !People.InWorld(_character))
        {
            return SkillStatus.Failed;
        }

        if (_walk != null)
        {
            var walkStatus = _walk.Tick();

            if (walkStatus == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            if (walkStatus == SkillStatus.Failed)
            {
                return SkillStatus.Failed;
            }

            _walk = null;
            _buying = true;
        }

        if (_buying)
        {
            _buying = false;
            return BuyHorse() ? SkillStatus.Done : SkillStatus.Failed;
        }

        return SkillStatus.Failed;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _buying = false;
    }

    public override void Resume(TimeSpan held) => _walk?.Resume(held);

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

    private bool BuyHorse()
    {
        var vendor = FindTrainer();

        if (vendor is not IVendor trader)
        {
            return false;
        }

        VendorDeal.RestockIfDue(vendor);

        foreach (var info in vendor.GetBuyInfo() ?? [])
        {
            if (info is not GenericBuyInfo buy || buy.Type != typeof(Horse))
            {
                continue;
            }

            var entity = buy.GetDisplayEntity();

            if (entity == null || !PackFunds.FundPack(_character, Math.Max(1, buy.Price)))
            {
                return false;
            }

            if (!trader.OnBuyItems(_character, [new BuyItemResponse(entity.Serial, 1)]))
            {
                return false;
            }

            FollowOwnedMounts();

            // The goal loop scores again as soon as a step ends, so the Mount step that
            // follows in the routine never ran. The horse spawns on the buyer's own
            // tile, so this is the one moment it is certain to be in reach.
            OwnedMounts.ClimbNearest(_character);
            return true;
        }

        return false;
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
