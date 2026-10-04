using Server;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// One quiet step around a ring. The engine counts the step against the steps the Stealth
/// check allowed and reveals the creeper when they run out; running would reveal at once,
/// so the creeper walks.
/// </summary>
public static class StealthCreep
{
    /// <summary>Steps toward the current ring point; moves to the next point on arrival. True when a step was taken.</summary>
    public static bool Step(SosariaCharacter creeper, Point3D anchor, ref int ringIndex)
    {
        if (creeper?.Map == null)
        {
            return false;
        }

        var target = StealthRules.RingPoint(anchor, ringIndex);

        if (NavMetric.Chebyshev(creeper.Location, target) == 0)
        {
            ringIndex++;
            target = StealthRules.RingPoint(anchor, ringIndex);
        }

        creeper.Motor.Running = false;
        return creeper.Motor.DoMoveOrSlide(creeper.GetDirectionTo(target));
    }
}
