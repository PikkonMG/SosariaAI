using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// After a stay indoors, follow the floor out to open ground so the next skill starts
/// from a place the route planner can work from.
/// </summary>
public sealed class BuildingLeave
{
    private GoToSkill _walk;

    public bool Begin(SosariaCharacter character)
    {
        var legs = character == null
            ? []
            : PathOut(character.Map, character.Location, NavWorld.GraphFor(character.HomeFacet));

        if (legs.Count == 0)
        {
            return false;
        }

        _walk = GoToSkill.FromPoints(legs, CharactersFile.DefaultGoToRange);
        return _walk.Begin(character);
    }

    public SkillStatus Tick() => _walk?.Tick() ?? SkillStatus.Done;

    public void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }

    /// <summary>
    /// Waypoints from a spot to the first open ground outside. Empty when the spot is
    /// already outside or no way out is found.
    /// </summary>
    public static IReadOnlyList<Point3D> PathOut(Map map, Point3D from, NavGraph graph)
    {
        if (map == null || map == Map.Internal || graph == null)
        {
            return [];
        }

        var walker = Standable.Walker(map);
        var street = Traveler.PrepareClearStart(graph, from, walker);

        return BuildingExit.Find(
            from,
            walker,
            (x, y, z) => IndoorTiles.IsBuilding(map, x, y, z),
            (x, y, z) => street.At(new Point3D(x, y, z))
        );
    }
}
