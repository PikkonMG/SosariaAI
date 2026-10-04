using System;
using System.Collections.Generic;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Logging;

namespace SosariaAI.Skills;

/// <summary>
/// Walks to a landmark near home the person has not seen, a shrine, a bank, a healer or a
/// town, looks at it a while, then walks out of any building it stands in. A murderer
/// looks only at places the guards do not watch: the walk to a guarded one is refused at
/// the first step, and a red on Buccaneer's Den chose the Peg Leg Inn and went nowhere.
/// </summary>
public sealed class SightseeSkill : Skill
{
    public static readonly TimeSpan LookDuration = TimeSpan.FromSeconds(60);

    private static readonly ILogger logger = SosariaLog.For(typeof(SightseeSkill));

    private readonly string _fallback;
    private readonly PlaceStay _stay = new(LookDuration, IdleWanderSkill.WanderChanceToNotMove, chat: null);
    private TravelSkill _walk;
    private SosariaCharacter _character;
    private string _chosen;

    public SightseeSkill(string destination) => _fallback = destination;

    public override string Name => SkillKinds.Sightsee;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _stay.Prepare(character);
        _chosen = PickPlace(character);

        if (_chosen == null)
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} has no place to look at near home", character.Name);
            }

            return false;
        }

        _walk = new TravelSkill(_chosen, CharactersFile.DefaultGoToRange);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} is going to look at {Place}", character.Name, _chosen);
        }

        return _walk.Begin(character);
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
                return SkillStatus.Failed;
            }

            _walk = null;
            _stay.Settle(CharactersFile.DefaultIdleRadius);
            _character.MarkPlace(_chosen ?? _character.LocationDescription);
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

    private string PickPlace(SosariaCharacter character)
    {
        var catalog = NavWorld.DestinationsFor(character.HomeFacet);
        var graph = NavWorld.GraphFor(character.HomeFacet);
        var home = character.HomeCorner;
        var radius = SosariaSettings.Characters?.Career?.LeisureRadius ??
                     CareerSettings.DefaultLeisureRadius;
        var sameFacet = SightseeRules.SameFacet(graph, character.HomeFacet);
        var homeNode = graph?.FindNearest(home)?.Name;
        var murderer = PkRules.IsRed(character.Kills);

        bool MayPick(Destination dest) =>
            dest != null &&
            PkRules.MayVisit(murderer, murderer && GuardCall.IsGuardedPlace(dest.Arrival, character.Map)) &&
            LeisureRules.MayPick(
                dest.Name,
                dest.Kind,
                dest.Location,
                home,
                radius,
                sameFacet,
                SightseeRules.CanRoute(graph, homeNode, dest.Node)
            );

        var names = new List<string>();

        if (catalog != null)
        {
            var all = catalog.All;

            for (var i = 0; i < all.Count; i++)
            {
                if (MayPick(all[i]))
                {
                    names.Add(all[i].Name);
                }
            }
        }

        var prefer = character.CurrentAmbition().Kind == SosariaAI.Behaviour.AmbitionKind.Place
            ? character.CurrentAmbition().Target
            : null;
        var fallback = MayPick(catalog?.Resolve(_fallback, home)) ? _fallback : null;
        return SightseeRules.PickUnseen(
                   names,
                   character.Memory.PlacesSeen(),
                   unchecked((int)character.Serial.Value),
                   prefer
               )
               ?? fallback;
    }
}
