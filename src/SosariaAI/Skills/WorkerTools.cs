using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// The tool each harvest trade cannot work without, and the shops whose vendors sell it.
/// The engine's weaponsmith is the only one with a hatchet, a pickaxe hangs at the smith,
/// the tinker and the weaponsmith, and a fishing pole at the fisherman. A lumberjack sent
/// to the blacksmith for a hatchet came back empty-handed three times and gave up.
/// </summary>
public static class WorkerTools
{
    private static readonly (string Kind, Type Tool, string[] Shops)[] Tools =
    [
        (SkillKinds.Mine, typeof(Pickaxe), [ShopFinder.SmithToken, ShopFinder.TinkerToken, ShopFinder.WeaponsmithToken]),
        (SkillKinds.Lumberjack, typeof(Hatchet), [ShopFinder.WeaponsmithToken]),
        (SkillKinds.Fish, typeof(FishingPole), [ShopFinder.FishermanToken])
    ];

    /// <summary>The tool a harvest skill needs, or null for a skill that needs none.</summary>
    public static Type ToolFor(string kind)
    {
        for (var i = 0; i < Tools.Length; i++)
        {
            if (Tools[i].Kind == kind)
            {
                return Tools[i].Tool;
            }
        }

        return null;
    }

    /// <summary>The shop kinds whose vendors sell <paramref name="tool"/>; empty for any other item.</summary>
    public static IReadOnlyList<string> ShopsFor(Type tool)
    {
        for (var i = 0; i < Tools.Length; i++)
        {
            if (Tools[i].Tool == tool)
            {
                return Tools[i].Shops;
            }
        }

        return [];
    }

    /// <summary>True when the person holds the item or carries it in its pack.</summary>
    public static bool Carries(Mobile person, Type type)
    {
        if (person == null || type == null)
        {
            return false;
        }

        if (person.Backpack?.FindItemByType(type) != null)
        {
            return true;
        }

        for (var i = 0; i < person.Items.Count; i++)
        {
            if (type.IsInstanceOfType(person.Items[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the item is a working tool a person keeps: a hatchet, a pickaxe, a fishing pole or a spellbook.</summary>
    public static bool IsWorkTool(Item item) => item is Hatchet or Pickaxe or FishingPole or Spellbook;

    /// <summary>True when the item is of a type the person's build kit names.</summary>
    public static bool IsKitItem(SosariaCharacter character, Item item) =>
        KitResolver.IsKitType(item, character?.Build?.Kit, name => AssemblyHandler.FindTypeByName(name));

    /// <summary>True when the skill needs no tool or the person carries its tool.</summary>
    public static bool HasToolFor(Mobile person, string kind) =>
        ToolFor(kind) is not { } tool || Carries(person, tool);

    /// <summary>The tools of the harvest trades this worker works and does not carry.</summary>
    public static IEnumerable<Type> Missing(SosariaCharacter worker)
    {
        for (var i = 0; i < Tools.Length; i++)
        {
            var (kind, tool, _) = Tools[i];
            var works = worker.Routine?.HasSkill(kind) == true || worker.Definition?.UsesSkill(kind) == true;

            if (works && !Carries(worker, tool))
            {
                yield return tool;
            }
        }
    }

    /// <summary>True when the worker lacks the tool of a harvest trade it works.</summary>
    public static bool MissesAny(SosariaCharacter worker)
    {
        foreach (var _ in Missing(worker))
        {
            return true;
        }

        return false;
    }
}
