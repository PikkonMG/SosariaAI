using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Behaviour;

/// <summary>
/// Finds bystanders for a scene: living characters near the spot, out of any fight, not in
/// another scene and not talking with a person at a keyboard. People at keyboards are never
/// cast. One range query per scene, made only when a scene is about to start.
/// </summary>
public static class SceneCast
{
    /// <summary>Up to <paramref name="max"/> bystanders around <paramref name="center"/> in random order, the lead first.</summary>
    public static List<SosariaCharacter> Around(
        SosariaCharacter lead,
        Point3D center,
        int range,
        int max,
        Func<SosariaCharacter, bool> fits = null
    )
    {
        var cast = new List<SosariaCharacter> { lead };

        if (lead?.Map == null || lead.Map == Map.Internal || max <= 0)
        {
            return cast;
        }

        var found = new List<SosariaCharacter>();
        var now = Core.Now;

        foreach (var mobile in lead.Map.GetMobilesInRange(center, range))
        {
            if (mobile is SosariaCharacter other && other != lead && Free(other, now) && fits?.Invoke(other) != false)
            {
                found.Add(other);
            }
        }

        for (var i = 0; i < found.Count && cast.Count <= max; i++)
        {
            var pick = i + Utility.Random(found.Count - i);
            (found[i], found[pick]) = (found[pick], found[i]);
            cast.Add(found[i]);
        }

        return cast;
    }

    public static bool Free(SosariaCharacter character, DateTime now) =>
        character is { Deleted: false, Alive: true, Hidden: false } &&
        character.Map != null && character.Map != Map.Internal &&
        character.Combatant == null &&
        !SceneRunner.IsBusy(character) &&
        !character.Conversation.IsActive(now);

    /// <summary>The bystanders a cast holds, not counting the lead.</summary>
    public static int Bystanders(IReadOnlyList<SosariaCharacter> cast) => Math.Max(0, cast.Count - 1);
}
