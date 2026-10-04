using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;

namespace SosariaAI.Behaviour;

/// <summary>
/// Legal facets, skills and places for the running ModernUO expansion.
/// Read Core.Expansion. Do not keep a second era switch in brain.json.
/// </summary>
public static class EraRules
{
    public const string TheHeartwood = "The Heartwood";
    public const string Sanctuary = "Sanctuary";
    public const string ThePaintedCaves = "The Painted Caves";
    public const string ThePrismOfLight = "The Prism of Light";
    public const string BlightedGrove = "Blighted Grove";
    public const string ThePalaceOfParoxysmus = "The Palace of Paroxysmus";
    public const string TwistedWeald = "Twisted Weald";
    public const string Bedlam = "Bedlam";
    public const string TheCitadel = "The Citadel";
    public const string Labyrinth = "Labyrinth";
    public const string YomotsuMines = "Yomotsu Mines";
    public const string FanDancersDojo = "Fan Dancer's Dojo";

    /// <summary>
    /// The town and dungeon regions of ModernUO's region data that a later expansion put on
    /// lands an older era already has, by the region name. The engine builds their regions,
    /// pads and spawns in every era: "shared" Felucca spawns hold Blighted Grove and
    /// Sanctuary, and the pad at (588,1637) opens the Grove on a Second Age shard, where
    /// delvers and the reds who chased them were stranded. Mondain's Legacy brought the Heartwood and its
    /// dungeons, the Twisted Weald and the Malas peerless halls; Samurai Empire the Yomotsu
    /// Mines and the Fan Dancer's Dojo.
    /// </summary>
    private static readonly Dictionary<string, Expansion> PlaceFloors = new(StringComparer.OrdinalIgnoreCase)
    {
        [TheHeartwood] = Expansion.ML,
        [Sanctuary] = Expansion.ML,
        [ThePaintedCaves] = Expansion.ML,
        [ThePrismOfLight] = Expansion.ML,
        [BlightedGrove] = Expansion.ML,
        [ThePalaceOfParoxysmus] = Expansion.ML,
        [TwistedWeald] = Expansion.ML,
        [Bedlam] = Expansion.ML,
        [TheCitadel] = Expansion.ML,
        [Labyrinth] = Expansion.ML,
        [YomotsuMines] = Expansion.SE,
        [FanDancersDojo] = Expansion.SE
    };

    public static Expansion Current() => Core.Expansion;

    public static bool FacetAllowed(string facet, Expansion expansion)
    {
        if (!FacetNames.TryCanonical(facet, out var name))
        {
            return false;
        }

        return name switch
        {
            FacetNames.Felucca => true,
            FacetNames.Trammel => expansion >= Expansion.UOR,
            FacetNames.Ilshenar => expansion >= Expansion.UOTD,
            FacetNames.Malas => expansion >= Expansion.AOS,
            FacetNames.Tokuno => expansion >= Expansion.SE,
            FacetNames.TerMur => expansion >= Expansion.SA,
            _ => false
        };
    }

    public static bool SkillAllowed(string skillKind, Expansion expansion)
    {
        if (string.IsNullOrWhiteSpace(skillKind))
        {
            return false;
        }

        return expansion >= FloorFor(skillKind);
    }

    private static Expansion FloorFor(string skillKind) =>
        skillKind switch
        {
            SkillKinds.Necro => SkillFloor(SkillName.Necromancy),
            _ => Expansion.None
        };

    /// <summary>
    /// The first expansion that has <paramref name="skill"/>: Age of Shadows brought
    /// necromancy, chivalry and focus; Samurai Empire bushido and ninjitsu; Mondain's
    /// Legacy spellweaving; Stygian Abyss mysticism, imbuing and throwing.
    /// </summary>
    public static Expansion SkillFloor(SkillName skill) =>
        skill switch
        {
            SkillName.Necromancy or SkillName.Chivalry or SkillName.Focus => Expansion.AOS,
            SkillName.Bushido or SkillName.Ninjitsu => Expansion.SE,
            SkillName.Spellweaving => Expansion.ML,
            SkillName.Mysticism or SkillName.Imbuing or SkillName.Throwing => Expansion.SA,
            _ => Expansion.None
        };

    /// <summary>
    /// The first expansion that has the town or dungeon region named <paramref name="regionName"/>.
    /// A place its facet brought with it, such as Doom on Malas, needs no floor of its own:
    /// <see cref="FacetAllowed"/> already keeps it out.
    /// </summary>
    public static Expansion PlaceFloor(string regionName) =>
        regionName != null && PlaceFloors.TryGetValue(regionName, out var floor) ? floor : Expansion.None;

    public static bool PlaceAllowed(string regionName, Expansion expansion) => expansion >= PlaceFloor(regionName);
}
