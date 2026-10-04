using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// Flattens authored routine steps into actions the goal loop can pick. Decide is not an action.
/// Every fighter with a catalog hall in reach may delve, every person may restock the
/// arrows, bandages and reagents it fights with, every caster may sit down to meditate,
/// and every person may pawn what it looted and bank the take, whether or not its authored
/// routines name the step: players of the era did all of them.
/// </summary>
public static class ActionCatalog
{
    public static List<ActionCandidate> From(
        CharacterDefinition definition,
        DestinationCatalog catalog,
        HuntHome hunt = null
    ) =>
        From(definition, catalog, Core.Expansion, hunt);

    public static List<ActionCandidate> From(
        CharacterDefinition definition,
        DestinationCatalog catalog,
        Expansion expansion,
        HuntHome hunt = null
    )
    {
        var found = new List<ActionCandidate>();

        if (definition == null)
        {
            return found;
        }

        var routines = definition.ResolvedRoutines();
        var noHallFits = hunt is { Dungeon: null, KnowsDoors: true };

        foreach (var pair in routines)
        {
            var steps = pair.Value;

            if (steps == null)
            {
                continue;
            }

            var required = RequiredPower(definition, pair.Key, catalog);

            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];

                if (step == null || string.IsNullOrWhiteSpace(step.Skill))
                {
                    continue;
                }

                if (step.Skill.Equals(SkillKinds.Decide, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!EraRules.SkillAllowed(step.Skill, expansion))
                {
                    continue;
                }

                // The catalog knows every door and found no hall for this home and power.
                // The authored dungeon would refuse at its first step, three times over.
                if (noHallFits && DungeonGround.IsDelve(step))
                {
                    continue;
                }

                var delve = DungeonGround.ForHome(step, hunt);

                found.Add(new ActionCandidate
                {
                    Id = ActionId.From(pair.Key, step, i),
                    SkillKind = step.Skill,
                    RoutineId = pair.Key,
                    Step = HuntGround.ForHome(delve, hunt),
                    // A catalog hall was picked within the character's power; its own
                    // difficulty replaces the authored dungeon's bar.
                    RequiredPower = ReferenceEquals(delve, step) ? required : hunt.Dungeon.Difficulty.GetValueOrDefault()
                });
            }
        }

        AddCatalogDelve(found, expansion, hunt);

        if (!found.Exists(action => action.SkillKind == SkillKinds.VendorBuy))
        {
            AddReactive(found, expansion, SkillKinds.VendorBuy, RestockId, RestockRoutine);
        }

        if (!found.Exists(action => action.SkillKind == SkillKinds.VendorSell))
        {
            AddReactive(found, expansion, SkillKinds.VendorSell, PawnId, PawnRoutine);
        }

        if (!found.Exists(action => action.SkillKind == SkillKinds.BankDeposit))
        {
            AddReactive(found, expansion, SkillKinds.BankDeposit, BankTripId, BankTripRoutine);
        }

        AddReactive(found, expansion, SkillKinds.Flee, "flee:Flee:0", "flee");
        AddReactive(found, expansion, SkillKinds.GoHome, "home:GoHome:0", "home");
        AddReactive(found, expansion, SkillKinds.Heal, "heal:Heal:0", "heal");

        if (!found.Exists(action => action.SkillKind == SkillKinds.Meditate))
        {
            AddReactive(found, expansion, SkillKinds.Meditate, MeditateId, MeditateRoutine);
        }
        AddReactive(found, expansion, SkillKinds.Conflict, "conflict:Conflict:0", "pk");
        AddReactive(found, expansion, SkillKinds.Arrive, "arrive:Arrive:0", "arrive");
        AddReactive(found, expansion, SkillKinds.Browse, "browse:Browse:0", "browse");
        AddReactive(found, expansion, SkillKinds.Follow, PartyFollowId, "party");
        return found;
    }

    private static int RequiredPower(
        CharacterDefinition definition,
        string routineId,
        DestinationCatalog catalog
    )
    {
        var choices = definition.Choices;

        if (choices == null)
        {
            return 0;
        }

        for (var i = 0; i < choices.Count; i++)
        {
            if (string.Equals(choices[i].Routine, routineId, StringComparison.OrdinalIgnoreCase))
            {
                return DecideChoice.RequiredPowerOf(choices[i], catalog);
            }
        }

        return 0;
    }

    /// <summary>The follow step any member of a real party takes behind its leader.</summary>
    public const string PartyFollowId = "party:Follow:0";

    /// <summary>The delve to the catalog hall for a person whose routines name no dungeon.</summary>
    public const string CatalogDelveId = "delve:Dungeon:0";

    public const string CatalogDelveRoutine = "delve";

    /// <summary>The supply run for a person whose routines name no shop step.</summary>
    public const string RestockId = "restock:VendorBuy:0";

    public const string RestockRoutine = "restock";

    /// <summary>The pawn run for a person whose routines name no sell step.</summary>
    public const string PawnId = "pawn:VendorSell:0";

    public const string PawnRoutine = "pawn";

    /// <summary>The bank errand for a person whose routines name no deposit step.</summary>
    public const string BankTripId = "bank:BankDeposit:0";

    public const string BankTripRoutine = "bank";

    /// <summary>The sit-down to meditate for a person whose routines name none.</summary>
    public const string MeditateId = "meditate:Meditate:0";

    public const string MeditateRoutine = "meditate";

    /// <summary>
    /// A fighter whose routines name no dungeon still delves the hall the catalog picked
    /// for it; a worker is barred by the scorer. The hall's difficulty is the power bar.
    /// </summary>
    private static void AddCatalogDelve(List<ActionCandidate> found, Expansion expansion, HuntHome hunt)
    {
        if (hunt?.Dungeon == null ||
            !EraRules.SkillAllowed(SkillKinds.Dungeon, expansion) ||
            found.Exists(action => action.SkillKind == SkillKinds.Dungeon))
        {
            return;
        }

        found.Add(new ActionCandidate
        {
            Id = new ActionId(CatalogDelveId),
            SkillKind = SkillKinds.Dungeon,
            RoutineId = CatalogDelveRoutine,
            Step = HuntGround.AtPlace(new SkillStepDefinition { Skill = SkillKinds.Dungeon }, hunt.Dungeon, keepsCrew: true),
            RequiredPower = hunt.Dungeon.Difficulty.GetValueOrDefault()
        });
    }

    public static bool IsPartyFollow(ActionCandidate candidate) =>
        candidate?.Id.Value == PartyFollowId;

    private static void AddReactive(
        List<ActionCandidate> found,
        Expansion expansion,
        string skillKind,
        string id,
        string routineId
    )
    {
        if (!EraRules.SkillAllowed(skillKind, expansion))
        {
            return;
        }

        found.Add(new ActionCandidate
        {
            Id = new ActionId(id),
            SkillKind = skillKind,
            RoutineId = routineId,
            Step = new SkillStepDefinition { Skill = skillKind }
        });
    }
}
