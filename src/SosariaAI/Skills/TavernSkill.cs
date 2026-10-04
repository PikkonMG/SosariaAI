using System;
using System.Collections.Generic;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Behaviour;

namespace SosariaAI.Skills;

public sealed class TavernSkill : Skill
{
    public const int StayChance = 6;

    /// <summary>How many inns to try, nearest first. The nearest keeper may stand upstairs.</summary>
    public const int MaxInnTries = 3;
    public static readonly TimeSpan Linger = TimeSpan.FromMinutes(2);

    private readonly string _destination;
    private readonly PlaceStay _stay;
    private TravelSkill _walk;
    private SosariaCharacter _character;
    private IReadOnlyList<Destination> _inns = [];
    private int _innIndex;
    private bool _triedByName;

    /// <summary>
    /// A tavern is where people talk: a few scripted exchanges, and at the game's night the
    /// patrons may talk in turn as a scene.
    /// </summary>
    public TavernSkill(string destination)
    {
        _destination = destination;
        _stay = new PlaceStay(
            Linger,
            StayChance,
            turns => Scenes.TavernNight(_character) || Meeting.TryChatNearby(_character, null, turns)
        );
    }

    public override string Name => SkillKinds.Tavern;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _stay.Prepare(character);
        _inns = NavWorld.DestinationsFor(character.HomeFacet)
            ?.NearestFirst(_destination, character.Location, MaxInnTries) ?? [];
        _innIndex = -1;
        _triedByName = false;
        return BeginNextInn();
    }

    public override SkillStatus Tick() => _stay.Finish(TickLinger());

    private SkillStatus TickLinger()
    {
        if (!_stay.Started)
        {
            var walk = _walk?.Tick() ?? SkillStatus.Failed;

            if (walk == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            if (walk == SkillStatus.Failed)
            {
                return BeginNextInn() ? SkillStatus.Running : SkillStatus.Failed;
            }

            _walk = null;
            _stay.Settle(CharactersFile.DefaultIdleRadius);
        }

        return _stay.Tick();
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _stay.Abort();
    }

    public override void Resume(TimeSpan held)
    {
        _stay.Resume(held);
        _walk?.Resume(held);
    }

    /// <summary>Walks to the next inn in the list, nearest first, skipping any with no route.</summary>
    private bool BeginNextInn()
    {
        while (++_innIndex < _inns.Count)
        {
            _walk = new TravelSkill(
                _inns[_innIndex].ApproachPoint(NavWorld.GraphFor(_character.HomeFacet)),
                NavLimits.ShopArrivalRange,
                arrivalFloor: true
            );

            if (_walk.Begin(_character))
            {
                return true;
            }
        }

        if (_inns.Count == 0 && !_triedByName)
        {
            _triedByName = true;
            var named = NavWorld.DestinationsFor(_character.HomeFacet)?.Resolve(_destination, _character.Location);

            // The name "tavern" resolved to the Britain tavern keeper for a Skara person.
            if (named == null ||
                HomeLeash.BeyondLeash(named.Arrival, _character.Location, HomeLeash.ConfiguredRadius()))
            {
                _walk = null;
                return _stay.LeaveNow();
            }

            _walk = new TravelSkill(
                named.ApproachPoint(NavWorld.GraphFor(_character.HomeFacet)),
                NavLimits.ShopArrivalRange,
                arrivalFloor: true
            );

            if (_walk.Begin(_character))
            {
                return true;
            }
        }

        _walk = null;
        return _stay.LeaveNow();
    }
}
