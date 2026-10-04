using Server;
using Server.Items;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// The real treasure maps, chests and SOS notes a character carries or dug up. The engine's
/// map knows its chest spot and facet; nothing here picks a site. World thread only.
/// </summary>
public static class TreasureMaps
{
    /// <summary>A dug chest rises on the map's own spot; the search looks this far around it.</summary>
    public const int ChestSearchRange = 1;

    /// <summary>The first treasure map in the pack whose treasure is still in the ground, or null.</summary>
    public static TreasureMap FirstOpen(Container pack)
    {
        foreach (var item in pack?.Items ?? [])
        {
            if (item is TreasureMap { Deleted: false, Completed: false } map)
            {
                return map;
            }
        }

        return null;
    }

    /// <summary>
    /// A map this character dug whose chest still stands with loot in it, or null: a hunt
    /// cut short by a fight or a new goal goes back for it.
    /// </summary>
    public static TreasureMap UnlootedDig(SosariaCharacter character)
    {
        foreach (var item in character?.Backpack?.Items ?? [])
        {
            if (item is TreasureMap { Deleted: false, Completed: true } map && map.CompletedBy == character &&
                ChestOf(character, map) is { Items.Count: > 0 })
            {
                return map;
            }
        }

        return null;
    }

    /// <summary>The chest the owner dug for this map, or null once it is gone.</summary>
    public static TreasureMapChest ChestOf(SosariaCharacter owner, TreasureMap map)
    {
        if (owner == null || map?.ChestMap == null || map.ChestMap == Map.Internal)
        {
            return null;
        }

        foreach (var item in map.ChestMap.GetItemsInRange(ChestPoint(map), ChestSearchRange))
        {
            if (item is TreasureMapChest { Deleted: false, Temporary: false } chest && chest.Owner == owner)
            {
                return chest;
            }
        }

        return null;
    }

    /// <summary>The chest spot on the ground of the map's facet.</summary>
    public static Point3D ChestPoint(TreasureMap map)
    {
        var at = map.ChestLocation;
        var z = map.ChestMap?.GetAverageZ(at.X, at.Y) ?? 0;
        return new Point3D(at.X, at.Y, z);
    }

    /// <summary>
    /// A map this character could finish: its facet is the one the character lives and
    /// stands on, it may dig it, and it can open the chest.
    /// </summary>
    public static bool MayHunt(SosariaCharacter character, TreasureMap map)
    {
        if (character == null || map is not { Deleted: false, Completed: false })
        {
            return false;
        }

        var rightFacet = map.ChestMap != null && character.Map == map.ChestMap &&
                         Map.Parse(character.HomeFacet) == map.ChestMap;

        return TreasureHuntRules.MayHunt(
            map.Level,
            rightFacet,
            map.Decoder == character,
            character.Skills.Cartography.Value,
            character.Skills.Lockpicking.Value,
            character.Skills.Magery.Value
        );
    }

    /// <summary>
    /// What the character would hold up for sale: a treasure map it cannot finish first, then
    /// an SOS note. Null when it carries neither.
    /// </summary>
    public static Item FirstForSale(SosariaCharacter character)
    {
        Item note = null;

        foreach (var item in character?.Backpack?.Items ?? [])
        {
            if (item is TreasureMap { Deleted: false, Completed: false } map && !MayHunt(character, map))
            {
                return map;
            }

            if (note == null && item is SOS { Deleted: false })
            {
                note = item;
            }
        }

        return note;
    }
}
