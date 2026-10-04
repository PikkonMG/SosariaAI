using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Recall by the real spell: home from the rune marked nearest home, or toward a goal from
/// the rune that serves it. A trip with no road to its goal takes a rune on a shorter trip
/// (<see cref="RecallRules.NoRoadMinTripTiles"/>). When the trip will not take, the skill
/// fails and the walk takes over.
/// </summary>
public sealed class RecallSkill : TravelCastSkill
{
    private readonly Point3D _goal;
    private readonly int _minTripTiles;

    public RecallSkill()
    {
    }

    public RecallSkill(Point3D goal, int minTripTiles = RecallRules.MinTripTiles)
    {
        _goal = goal;
        _minTripTiles = minTripTiles;
    }

    public override string Name => RecallRules.Kind;

    protected override bool BeginCast(SosariaCharacter character) =>
        _goal == Point3D.Zero
            ? RecallRules.TryRecallHome(character)
            : RecallRules.TryRecallToward(character, _goal, _minTripTiles);
}
