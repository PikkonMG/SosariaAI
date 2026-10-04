using System;
using System.Collections.Generic;

namespace SosariaAI.Behaviour;

/// <summary>The kind of place a 1999 PvP hot spot was.</summary>
public enum HotSpotKind
{
    /// <summary>A public moongate: travellers step off the pad into open ground.</summary>
    Moongate,

    /// <summary>A busy dungeon door, where crews come out hurt and loaded.</summary>
    DungeonDoor,

    /// <summary>A town graveyard out of the guards' reach.</summary>
    Graveyard,

    /// <summary>Buccaneer's Den, the reds' own town.</summary>
    Den
}

/// <summary>
/// Where Felucca's PvP happened (see <see cref="HotSpots"/>): the ground round a moongate
/// (Yew's gate in the woods above all), the doors of Despise, Covetous,
/// Destard and Shame, the Britain graveyard and Buccaneer's Den. Red gangs camp one on foot
/// for a while, jump the travellers who come by, and move on; blue sweeps and war bands ride
/// out to them; PvP guilds keep a few houses on their outskirts. Which moongates count is read
/// from the ground round each pad, not listed. Pure.
/// </summary>
public static class HotSpotRules
{
    public const int NoSpot = -1;

    /// <summary>
    /// The Yew moongate's name number in the engine's moongate list (PublicMoongate.cs: 1012006,
    /// "Yew"): the gate in the woods, the busiest killing ground of the era.
    /// </summary>
    public const int YewGateNumber = 1012006;

    /// <summary>The dungeons whose doors drew the most traffic, and so the most reds.</summary>
    public static readonly string[] BusyDungeons = ["Despise", "Covetous", "Destard", "Shame"];

    /// <summary>The graveyards that were hot spots, by their catalog names.</summary>
    public static readonly string[] Graveyards = ["Britain Cemetery"];

    /// <summary>Camp spots stand this far out from a moongate: past its guard box and its peace radius.</summary>
    public const int GateCampTiles = 16;

    /// <summary>Camp spots stand this far out from a door, a graveyard or the Den's square.</summary>
    public const int NearCampTiles = 6;

    /// <summary>The compass points a camp is looked for on.</summary>
    public const int CampDirections = 8;

    /// <summary>A moongate counts when at least this many of its compass points lie open past the guards.</summary>
    public const int MinOpenCamps = CampDirections / 2;

    public const int YewGateWeight = 5;
    public const int GateWeight = 2;
    public const int DoorWeight = 2;
    public const int GraveyardWeight = 1;
    public const int DenWeight = 2;

    /// <summary>PvP houses on the outskirts of the Yew gate.</summary>
    public const int YewGateHouses = 3;

    /// <summary>PvP houses on the outskirts of any other moongate or door.</summary>
    public const int SpotHouses = 1;

    /// <summary>
    /// A red walks to a hot spot at most this far, the hops through the moongates it may take
    /// counted as the walk to the pad and on from the far one; a farther spot is left to reds nearer it.
    /// </summary>
    public const int RedReachTiles = 900;

    /// <summary>
    /// A hot spot is in a red's reach when the red can walk there, murderer's roads only and
    /// not too far, or when it carries a rune that lands by the spot and can cast the recall.
    /// A spot across water it cannot cross is no camp at all: from Buccaneer's Den 66 of 77
    /// failed camps had no route off the island.
    /// </summary>
    public static bool InReach(bool walkable, int distance, bool recallable) =>
        walkable && distance <= RedReachTiles || recallable;

    /// <summary>Gangs keep one hot spot this long before the rotation moves them on.</summary>
    public static readonly TimeSpan CampSlot = TimeSpan.FromMinutes(20);

    private const int GangSalt = 7919;
    private const int SlotSalt = 104729;

    /// <summary>
    /// A moongate is a hot spot when its pad has no guards and enough of the ground round it
    /// lies where the guards never come. Its traffic makes the spot: in 1999 six of the eight
    /// Felucca pads had no guards, and a rule that asked for a guarded pad found no gate at all.
    /// A guarded pad (Britain and Moonglow in the era, see <see cref="MoongateGuards"/>) is no
    /// spot: a red that steps off a run there dies to the guards. The Den's own gate is no spot
    /// either: the Den is the reds' home, where no red strikes.
    /// </summary>
    public static bool GateIsHotSpot(bool inBucsDen, bool padGuarded, int openCamps) =>
        !inBucsDen && !padGuarded && openCamps >= MinOpenCamps;

    public static int WeightOf(HotSpotKind kind, bool yewGate) =>
        kind switch
        {
            HotSpotKind.Moongate => yewGate ? YewGateWeight : GateWeight,
            HotSpotKind.DungeonDoor => DoorWeight,
            HotSpotKind.Graveyard => GraveyardWeight,
            _ => DenWeight
        };

    /// <summary>
    /// How many PvP houses stand on a spot's outskirts: three by the Yew gate, one by any other
    /// gate or door, none at a graveyard by town or in the Den, where the engine allows no house.
    /// </summary>
    public static int HousesFor(HotSpotKind kind, bool yewGate) =>
        kind switch
        {
            HotSpotKind.Moongate => yewGate ? YewGateHouses : SpotHouses,
            HotSpotKind.DungeonDoor => SpotHouses,
            _ => 0
        };

    /// <summary>
    /// A spot a red's run rides out to, and an ordinary blue sweep rides to: any but the Den.
    /// The Den is the reds' home, where a red banks and hangs about between runs and may strike
    /// no one; blues ride into it only on a planned Den raid (see <see cref="PartyRoads"/>).
    /// </summary>
    public static bool IsRunCamp(HotSpotKind kind) => kind != HotSpotKind.Den;

    public static bool IsBusyDungeon(string dungeon) => Contains(BusyDungeons, dungeon);

    public static bool IsHotGraveyard(string name) => Contains(Graveyards, name);

    public static long SlotOf(DateTime now) => now.Ticks / CampSlot.Ticks;

    /// <summary>
    /// The spot a gang camps in this slot: a weighted draw seeded by the gang and the slot, so
    /// a gang's mates agree on it, gangs spread over the spots, and the spot changes with the
    /// slot. <see cref="NoSpot"/> when no spot has weight.
    /// </summary>
    public static int PickIndex(IReadOnlyList<int> weights, int gang, long slot)
    {
        var total = 0;

        for (var i = 0; i < (weights?.Count ?? 0); i++)
        {
            total += Math.Max(0, weights[i]);
        }

        if (total == 0)
        {
            return NoSpot;
        }

        var seed = unchecked((long)(gang + 1) * GangSalt + slot * SlotSalt);
        var roll = (int)(((seed % total) + total) % total);

        for (var i = 0; i < weights.Count; i++)
        {
            roll -= Math.Max(0, weights[i]);

            if (roll < 0)
            {
                return i;
            }
        }

        return NoSpot;
    }

    /// <summary>The camp of a spot a gang takes: gangs on one spot spread over its camps.</summary>
    public static int CampIndex(int gang, int camps) => camps <= 0 ? NoSpot : ((gang % camps) + camps) % camps;

    private static bool Contains(string[] names, string name)
    {
        for (var i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], name?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
