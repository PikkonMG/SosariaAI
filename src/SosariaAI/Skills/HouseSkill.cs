using Server;
using Server.Items;
using Server.Logging;
using Server.Multis;
using Server.Multis.Deeds;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Logging;

namespace SosariaAI.Skills;

/// <summary>
/// Buys the allowed small house, or looks in on the one the character owns. A buyer walks to
/// the plot and places a cottage. An owner walks to its house and uses the sign, the way a
/// player kept a house from decay (<see cref="HouseVisit"/>).
/// </summary>
public sealed class HouseSkill : Skill
{
    private const string GoneWhy = "the house is gone";
    private const string SignWhy = "the house sign did not refresh the house";

    private static readonly ILogger logger = SosariaLog.For(typeof(HouseSkill));

    private SosariaCharacter _character;
    private TravelSkill _walk;
    private Point3D _plot;
    private BaseHouse _house;

    public override string Name => SkillKinds.House;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        HouseVisit.ForgetFallen(character);
        _house = HouseVisit.OwnedHouse(character);
        _plot = _house != null ? HouseVisit.Doorstep(character, _house) : PlotFor(character);

        if (_plot == Point3D.Zero || _house == null && character.HouseSerial > HouseRules.NoHouseSerial)
        {
            return false;
        }

        _walk = new TravelSkill(_plot, HouseRules.PlaceArrivalTiles);
        return _walk.Begin(character);
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted)
        {
            return SkillStatus.Failed;
        }

        var walk = _walk?.Tick() ?? SkillStatus.Failed;

        if (walk == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        if (walk == SkillStatus.Failed)
        {
            return SkillStatus.Failed;
        }

        _walk = null;
        return Arrive();
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }

    public override void Resume(System.TimeSpan held) => _walk?.Resume(held);

    private SkillStatus Arrive()
    {
        if (!HouseRules.NearPlot(_character.Location, _plot, HouseRules.PlaceArrivalTiles))
        {
            return SkillStatus.Failed;
        }

        if (_house != null)
        {
            return LookIn();
        }

        return Place() ? SkillStatus.Done : SkillStatus.Failed;
    }

    /// <summary>
    /// The owner at its house records it, then uses the sign. Done once the house wants no
    /// visit: refreshed, or of a kind no visit refreshes.
    /// </summary>
    private SkillStatus LookIn()
    {
        if (_house.Deleted)
        {
            return Fail(GoneWhy);
        }

        if (_character.HouseSerial != (int)_house.Serial.Value)
        {
            _character.HouseSerial = (int)_house.Serial.Value;
            _character.HouseLocation = _house.Location;
        }

        HouseVisit.UseSign(_character, _house);

        if (HouseVisit.Due(_house))
        {
            return Fail(SignWhy);
        }

        WorldPlay.Log($"{_character.Name} looked in on its house at {_house.Location}");
        return SkillStatus.Done;
    }

    private bool Place()
    {
        var existing = HouseVisit.OwnedHouse(_character);

        if (existing != null)
        {
            return RecordHouse(existing, existing.Location, spendGold: 0);
        }

        var need = SosariaSettings.Characters?.Career?.HouseGold ?? CareerSettings.DefaultHouseGold;

        if (!HouseRules.CanBuy(BankTeller.CoinsHeld(_character), need, _character.HouseSerial > HouseRules.NoHouseSerial))
        {
            return false;
        }

        var deed = new ThatchedRoofCottageDeed();
        var spot = FindValidSpot(deed);

        if (spot == Point3D.Zero)
        {
            DeleteDeed(deed);
            return false;
        }

        if (!_character.PlaceInBackpack(deed))
        {
            logger.Warning(
                "{Name} house deed did not fit in pack at {Location}",
                _character.Name,
                _character.Location
            );
            DeleteDeed(deed);
            return false;
        }

        deed.OnPlacement(_character, spot);
        var house = FindPlacedHouse(spot);

        if (house == null)
        {
            logger.Warning("{Name} house OnPlacement rejected {Location}", _character.Name, spot);
            DeleteDeed(deed);
            return false;
        }

        return RecordHouse(house, spot, need);
    }

    private Point3D FindValidSpot(HouseDeed deed)
    {
        HousePlacementResult last = HousePlacementResult.BadLand;
        var map = _character.Map;

        if (People.InWorld(_character))
        {
            foreach (var spot in HouseRules.CandidateSpots(_character.Location, _plot))
            {
                var z = map.GetAverageZ(spot.X, spot.Y);
                var click = new Point3D(spot.X, spot.Y, z);
                var center = HouseRules.PlacementCenter(click, deed.Offset);
                last = HousePlacement.Check(_character, deed.MultiID, center, out _, deed.HouseDirection);

                if (last == HousePlacementResult.Valid)
                {
                    return click;
                }
            }
        }

        logger.Warning(
            "{Name} could not place a house near {Location}: {Result}",
            _character.Name,
            _character.Location,
            last
        );
        return Point3D.Zero;
    }

    private BaseHouse FindPlacedHouse(Point3D spot)
    {
        var atSpot = BaseHouse.FindHouseAt(spot, _character.Map, PersonBody.Height);

        if (atSpot is { Deleted: false })
        {
            return atSpot;
        }

        var atFeet = BaseHouse.FindHouseAt(_character);

        if (atFeet is { Deleted: false })
        {
            return atFeet;
        }

        return HouseVisit.OwnedHouse(_character);
    }

    private bool RecordHouse(BaseHouse house, Point3D spot, int spendGold)
    {
        if (spendGold > 0)
        {
            BankTeller.PayPackThenBank(_character, spendGold);
        }

        _character.HouseSerial = (int)house.Serial.Value;
        _character.HouseLocation = spot;

        var atFeet = BaseHouse.FindHouseAt(_character) != null
            || BaseHouse.FindHouseAt(spot, _character.Map, PersonBody.Height) != null;
        var serialLive = HouseRules.BySerial(_character.HouseSerial) is { Deleted: false };

        if (!HouseRules.Stands(_character.HouseSerial, atFeet, serialLive))
        {
            logger.Warning(
                "{Name} house serial {Serial} does not stand at {Location}",
                _character.Name,
                _character.HouseSerial,
                spot
            );
            return false;
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} placed a house at {Location}", _character.Name, spot);
        }

        AdventureTracker.Shared.House(_character, Core.Now);
        _character.SpeakAloud(HouseRules.BoughtLine(spot.ToString()));
        return true;
    }

    private static void DeleteDeed(HouseDeed deed)
    {
        if (deed is { Deleted: false })
        {
            deed.Delete();
        }
    }

    private static Point3D PlotFor(SosariaCharacter character)
    {
        var plots = SosariaSettings.Characters?.HousePlots;

        if (plots == null || plots.Count == 0)
        {
            return HouseRules.DefaultPlot();
        }

        for (var i = 0; i < plots.Count; i++)
        {
            if (plots[i] != null &&
                (string.IsNullOrWhiteSpace(plots[i].Map) ||
                 plots[i].Map.Equals(character.HomeFacet, System.StringComparison.OrdinalIgnoreCase)))
            {
                return plots[i].Location;
            }
        }

        return HouseRules.DefaultPlot();
    }
}
