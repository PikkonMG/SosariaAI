using Server;
using Server.Items;
using Server.Logging;
using Server.Multis;
using Server.Multis.Deeds;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;
using SosariaAI.Logging;

namespace SosariaAI.Skills;

/// <summary>
/// Buys the allowed small house, or goes home to the one the character owns. A buyer walks to
/// the plot and places a cottage. An owner travels home, by its home rune when it carries one,
/// uses the sign the way a player kept a house from decay (<see cref="HouseVisit"/>), keeps a
/// rune for its door in its book, and stays in a while (<see cref="HomeStaySkill"/>).
/// </summary>
public sealed class HouseSkill : Skill
{
    private const string GoneWhy = "the house is gone";
    private const string SignWhy = "the house sign did not refresh the house";

    private static readonly ILogger logger = SosariaLog.For(typeof(HouseSkill));

    private SosariaCharacter _character;
    private TravelSkill _walk;
    private Point3D _plot;
    private HousePlot _buyPlot;
    private BaseHouse _house;

    public override string Name => SkillKinds.House;

    /// <summary>The owner's stay at home, once it reached its door; null before.</summary>
    public HomeStaySkill Stay { get; private set; }

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        Stay = null;
        HouseVisit.ForgetFallen(character);
        _house = HouseVisit.OwnedHouse(character);
        _buyPlot = _house != null ? null : HousePlots.FreeFor(character);
        _plot = _house != null ? HouseVisit.Doorstep(character, _house) : _buyPlot?.Location ?? Point3D.Zero;

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

        if (Stay != null)
        {
            return Stay.Tick();
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
        Stay?.Abort();
    }

    public override void Resume(System.TimeSpan held)
    {
        _walk?.Resume(held);
        Stay?.Resume(held);
    }

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
    /// The owner at its house records it and uses the sign. Once the house wants no visit,
    /// refreshed or of a kind no visit refreshes, it keeps a rune for its door and goes in.
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

        if (HouseDoorway.Of(_house) is not { } way)
        {
            return SkillStatus.Done;
        }

        RuneKit.MarkHome(_character, way.Front);
        Stay = new HomeStaySkill(way);
        return Stay.Begin(_character) ? SkillStatus.Running : Fail(Stay.FailReason ?? "could not go in");
    }

    private bool Place()
    {
        var existing = HouseVisit.OwnedHouse(_character);

        if (existing != null)
        {
            return RecordHouse(existing, existing.Location, spendGold: 0);
        }

        var need = HousePlots.PriceFor(_character);

        if (!HouseRules.CanBuy(BankTeller.CoinsHeld(_character), need, _character.HouseSerial > HouseRules.NoHouseSerial))
        {
            return false;
        }

        var deed = HousePlots.DeedFor(_character);
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
            foreach (var spot in HouseRules.CandidateSpots(_character.Location, _plot, _buyPlot.Clear, _buyPlot.Reach))
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
        _character.SpeakAloud(HouseRules.BoughtLine(HouseWantRules.Target(HouseWantRules.SizeFor(HousePlots.Seed(_character))), spot.ToString()));
        return true;
    }

    private static void DeleteDeed(HouseDeed deed)
    {
        if (deed is { Deleted: false })
        {
            deed.Delete();
        }
    }
}
