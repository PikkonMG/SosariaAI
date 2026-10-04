using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Multis;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Place a player vendor in a house this character already owns, stock it at the market
/// table's prices, and collect what it took in (<see cref="PlayerVendorRules"/>).
/// Being away from the house does not clear ownership. At the house the owner uses its sign
/// first, as any owner home again did (<see cref="HouseVisit"/>).
/// </summary>
public sealed class PlayerVendorSkill : Skill
{
    private static readonly ILogger logger = SosariaLog.For(typeof(PlayerVendorSkill));

    private SosariaCharacter _owner;
    private TravelSkill _walk;

    public override string Name => SkillKinds.PlayerVendor;

    public override bool Begin(SosariaCharacter character)
    {
        _owner = character;
        _walk = null;

        if (character is not { Deleted: false } || !character.HouseOwned())
        {
            return false;
        }

        var house = HouseFor(character);

        if (house == null)
        {
            return false;
        }

        if (BaseHouse.FindHouseAt(character) == house)
        {
            return true;
        }

        _walk = new TravelSkill(HouseVisit.Doorstep(character, house), HouseRules.PlaceArrivalTiles);
        return _walk.Begin(character);
    }

    public override SkillStatus Tick()
    {
        if (_owner == null || _owner.Deleted || !People.InWorld(_owner))
        {
            return SkillStatus.Failed;
        }

        if (_walk != null)
        {
            var walk = _walk.Tick();

            if (walk == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            if (walk != SkillStatus.Done)
            {
                return SkillStatus.Failed;
            }

            _walk = null;
        }

        var house = HouseFor(_owner);

        if (house == null)
        {
            return SkillStatus.Failed;
        }

        HouseVisit.UseSign(_owner, house);
        var vendor = PlayerVendorRules.OwnedVendor(_owner.VendorSerial) ?? FirstVendor(house);

        if (vendor == null)
        {
            if (!PlayerVendorRules.MayPlace(_owner.HouseSerial, house.PlayerVendors.Count))
            {
                return SkillStatus.Failed;
            }

            vendor = new PlayerVendor(_owner, house);
            vendor.MoveToWorld(_owner.Location, _owner.Map);
            _owner.VendorSerial = (int)vendor.Serial.Value;
        }

        var stocked = PlayerVendorRules.RestockFrom(_owner, vendor);
        var collected = PlayerVendorRules.Collect(_owner, vendor);

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} tends its vendor at {Location}: {Stocked}, collected {Gold} gold",
                _owner.Name,
                vendor.Location,
                stocked ? "put up new stock" : "no new stock",
                collected
            );
        }

        return SkillStatus.Done;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _owner = null;
    }

    private static BaseHouse HouseFor(SosariaCharacter character)
    {
        var live = HouseRules.BySerial(character.HouseSerial);

        if (live is { Deleted: false })
        {
            return live;
        }

        return BaseHouse.FindHouseAt(character);
    }

    private static PlayerVendor FirstVendor(BaseHouse house)
    {
        if (house?.PlayerVendors == null || house.PlayerVendors.Count == 0)
        {
            return null;
        }

        return house.PlayerVendors[0];
    }
}
