using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>
/// Combat kit and fresh-build apply once. Worker tools still top up after that.
/// Clothes (shirt, pants, boots) are not combat kit pieces.
/// Kit pieces with no wear layer (arrows, reagents) go to the backpack, where
/// BaseRanged and Spell look for them. A ghost is not dressed; the corpse holds the gear.
/// </summary>
public static class KitOnceRules
{
    /// <summary>
    /// A starting weapon and spellbook are newbied, as a 1999 character's were: they stay with
    /// their owner through a death. Lost with the body, they left 171 of 504 blues of one run
    /// fighting bare-handed to its end, and a mage that bought a blank book never cast again.
    /// </summary>
    public static bool IsNewbiedKitPiece(bool weapon, bool spellbook) => weapon || spellbook;

    public const string Hatchet = "Hatchet";
    public const string Pickaxe = "Pickaxe";
    public const string FishingPole = "FishingPole";

    private static readonly HashSet<string> WorkerTools = new(StringComparer.OrdinalIgnoreCase)
    {
        Hatchet,
        Pickaxe,
        FishingPole
    };

    private static readonly HashSet<string> Clothes = new(StringComparer.OrdinalIgnoreCase)
    {
        OutfitRules.Shirt,
        OutfitRules.LongPants,
        OutfitRules.Boots
    };

    public static bool ShouldGrantCombatKit(bool careerStarted) => !careerStarted;

    public static bool ShouldApplyFreshBuild(bool careerStarted) => !careerStarted;

    public static bool IsWorkerTool(string typeName) =>
        !string.IsNullOrWhiteSpace(typeName) && WorkerTools.Contains(typeName);

    public static bool IsCombatKitPiece(string typeName) =>
        !string.IsNullOrWhiteSpace(typeName) &&
        !IsWorkerTool(typeName) &&
        !Clothes.Contains(typeName);

    public static bool GoesInBackpack(Layer layer) => layer == Layer.Invalid;

    public static bool ShouldDress(bool isGhost) => !isGhost;
}
