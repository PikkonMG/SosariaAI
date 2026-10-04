using Server;
using Server.Items;
using Server.Logging;
using Server.Multis;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Logging;

namespace SosariaAI.Skills;

/// <summary>
/// Own a T2A small boat, board it, move it, and reuse it. A failed place does not pay. An owner
/// boards the way a player does: it walks within reach of a plank, unlocks the plank with its
/// ship key, opens it and uses it, and the plank puts it aboard.
/// </summary>
public sealed class BoatSkill : Skill
{
    private const string WalkFailedWhy = "the walk to the boat failed";
    private const string BoatGoneWhy = "the boat is gone";
    private const string NoPlankWhy = "the boat has no plank";
    private const string NoKeyWhy = "no key in the pack opens the plank";
    private const string PlankShutWhy = "the plank did not open";
    private const string NotAboardWhy = "the plank did not take it aboard";

    private static readonly ILogger logger = SosariaLog.For(typeof(BoatSkill));

    private SosariaCharacter _character;
    private TravelSkill _walk;
    private bool _reuse;

    public override string Name => SkillKinds.Boat;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _reuse = false;

        var owned = BoatRules.OwnedBoat(character.BoatSerial);

        if (owned != null)
        {
            _reuse = true;
            var plank = BoatRules.NearerPlank(owned, character.Location);
            return plank != null ? StartWalk(plank.Location, BoatRules.PlankUseRange) : CannotStart(NoPlankWhy);
        }

        if (BankTeller.CoinsHeld(character) < AmbitionRules.DefaultBoatGold)
        {
            return false;
        }

        if (BaseBoat.FindBoatAt(character.Location, character.Map) != null)
        {
            return false;
        }

        return StartWalk(CharactersFile.BritainDock, CharactersFile.DefaultGoToRange);
    }

    public override SkillStatus Tick()
    {
        if (_walk == null || _character == null)
        {
            return SkillStatus.Failed;
        }

        var walk = _walk.Tick();

        if (walk == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        if (walk != SkillStatus.Done)
        {
            return Fail(TravelSkill.WithWalkWhy(WalkFailedWhy, _walk));
        }

        return _reuse ? Board() : Place();
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _character = null;
    }

    private bool StartWalk(Point3D goal, int range)
    {
        _walk = new TravelSkill(goal, range);
        return _walk.Begin(_character) || CannotStart(TravelSkill.WithWalkWhy(WalkFailedWhy, _walk));
    }

    /// <summary>
    /// Within reach of a plank, the owner unlocks it with its ship key, uses it once to open it
    /// and once more to step onto it (Plank.OnDoubleClick), then sails off.
    /// </summary>
    private SkillStatus Board()
    {
        var boat = BoatRules.OwnedBoat(_character.BoatSerial);

        if (boat == null)
        {
            return Fail(BoatGoneWhy);
        }

        var plank = BoatRules.NearerPlank(boat, _character.Location);

        if (plank == null)
        {
            return Fail(NoPlankWhy);
        }

        if (plank.Locked && !Unlock(plank))
        {
            return Fail(NoKeyWhy);
        }

        if (!plank.IsOpen)
        {
            _character.Use(plank);
        }

        if (!plank.IsOpen)
        {
            return Fail(PlankShutWhy);
        }

        _character.Use(plank);

        if (!boat.Contains(_character))
        {
            return Fail(NotAboardWhy);
        }

        boat.RaiseAnchor(false);
        boat.StartMove(boat.Facing, fast: false);
        return SkillStatus.Done;
    }

    // The ship key the deed gave goes on the plank the way a player aims a key: use it, then target the plank.
    private bool Unlock(Plank plank)
    {
        var key = _character.Backpack?.FindItemByType<Key>(true, held => held.KeyValue == plank.KeyValue);

        if (key == null)
        {
            return false;
        }

        _character.Use(key);
        _character.Target?.Invoke(_character, plank);
        return !plank.Locked;
    }

    private SkillStatus Place()
    {
        var need = AmbitionRules.DefaultBoatGold;
        var gold = BankTeller.CoinsHeld(_character);

        if (gold < need)
        {
            logger.Warning("{Name} boat gold {Gold} need {Need}", _character.Name, gold, need);
            return SkillStatus.Failed;
        }

        if (!BoatRules.TryFindFit(_character.Map, BoatRules.BritainOpenWater, out var fit))
        {
            return SkillStatus.Failed;
        }

        var deed = new SmallBoatDeed();
        _character.AddToBackpack(deed);
        deed.OnPlacement(_character, fit);

        var boat = BaseBoat.FindBoatAt(fit, _character.Map);

        if (boat == null)
        {
            logger.Warning("{Name} boat OnPlacement rejected {Fit}", _character.Name, fit);
            deed.Delete();
            return SkillStatus.Failed;
        }

        BankTeller.PayPackThenBank(_character, need);
        _character.BoatSerial = (int)boat.Serial.Value;
        return SkillStatus.Done;
    }
}
