using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// The part every stand-about step shares: pick a spot with room (<see cref="LoiterSpot"/>),
/// walk there by road when it is out of the ring, then stand about inside the ring at the
/// step's own pace. World thread only.
/// </summary>
public sealed class LoiterStay
{
    private SosariaCharacter _character;
    private TravelSkill _walk;
    private Point3D _spot;
    private int _radius;
    private int _stayWeight;

    /// <summary>The spot the person stands about at.</summary>
    public Point3D Spot => _spot;

    /// <summary>True while the person still walks to its spot.</summary>
    public bool Walking => _walk != null;

    /// <summary>The walk to a spot ends this close to it: inside the ring, not all on its centre tile.</summary>
    public static int WalkRange(int radius) =>
        Math.Max(CharactersFile.DefaultGoToRange, Math.Min(radius, LoiterSpotRules.SpotRadius));

    /// <summary>False when the spot is out of the ring and no walk there can start.</summary>
    public bool Begin(SosariaCharacter character, Point3D wanted, int radius, int stayWeight)
    {
        _character = character;
        _radius = radius;
        _stayWeight = stayWeight;
        _spot = LoiterSpot.Choose(character, wanted);
        _walk = null;

        if (NavMetric.Chebyshev(character.Location, _spot) <= radius)
        {
            Settle();
            return true;
        }

        _walk = new TravelSkill(_spot, WalkRange(radius));

        if (_walk.Begin(character))
        {
            return true;
        }

        _walk = null;
        return false;
    }

    /// <summary>
    /// Running while the walk runs or the person stands about; Failed when the walk fails.
    /// <paramref name="settled"/> is true on the tick the person reached its spot.
    /// </summary>
    public SkillStatus Tick(out bool settled)
    {
        settled = false;

        if (_walk != null)
        {
            var walk = _walk.Tick();

            if (walk == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _walk = null;

            if (walk == SkillStatus.Failed)
            {
                return SkillStatus.Failed;
            }

            Settle();
            settled = true;
        }

        _character.Motor.LoiterInHome(_stayWeight);
        return SkillStatus.Running;
    }

    public void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }

    public void Resume(TimeSpan held) => _walk?.Resume(held);

    private void Settle()
    {
        _character.Home = _spot;
        _character.RangeHome = _radius;
    }
}
