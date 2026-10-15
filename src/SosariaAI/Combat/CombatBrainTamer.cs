using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Combat;

/// <summary>A tamer's own moves in its pets' fight (<see cref="TamerTacticsRules"/>).</summary>
public static partial class CombatBrain
{
    private const string ComeOutLine = "come out";

    /// <summary>
    /// A tamer with pets out works their fight before it casts at the foe: "all kill" first, a
    /// pet near death called off with the tamer running beside it, Invisibility or Hiding when
    /// the foe turns on it, a still stand while hidden, a heal on the hurt pet to come out, and a
    /// hold out of reach until a pet is on the foe. True when that took the think.
    /// </summary>
    private static bool TryTamerTactics(
        SosariaCharacter character,
        Memory memory,
        CasterTactics tactics,
        Mobile foe,
        int distance,
        CombatStance stance,
        int safeCircle
    )
    {
        if (!PetRules.KeepsPets(character.PersonProfile.Class))
        {
            return false;
        }

        var pets = PetKeeper.PetsOut(character);

        if (pets.Count == 0)
        {
            return false;
        }

        PetOrders.Direct(character, force: false);

        var ready = false;
        BaseCreature weakest = null;
        var weakestHits = TamerTacticsRules.FullPetHits;
        BaseCreature hurt = null;

        for (var i = 0; i < pets.Count; i++)
        {
            var pet = pets[i];
            var hits = TamerTacticsRules.PetHits(pet.Hits, pet.HitsMax);
            ready |= TamerTacticsRules.PetReady(pet.Hits, pet.HitsMax);

            if (pet.Combatant == foe && (weakest == null || hits < weakestHits))
            {
                weakest = pet;
                weakestHits = hits;
            }

            if (PetRules.NeedsVet(pet.Hits, pet.HitsMax, pet.Poisoned) &&
                (hurt == null || hits < TamerTacticsRules.PetHits(hurt.Hits, hurt.HitsMax)))
            {
                hurt = pet;
            }
        }

        var now = Core.TickCount;
        var foeOnTamer = foe.Combatant == character;
        var sight = new TamerSight(
            ready,
            weakest != null,
            weakestHits,
            hurt != null,
            hurt != null && CanTend(character, hurt),
            foeOnTamer,
            character.Hidden,
            tactics.HiddenSince == 0 ? 0 : now - tactics.HiddenSince,
            foeOnTamer && CanVanish(character, memory),
            foeOnTamer && CanHideFrom(character, memory, foe),
            now - tactics.FightSince
        );

        switch (TamerTacticsRules.Next(sight))
        {
            case TamerMove.SavePet:
            {
                SavePet(character, memory, foe, weakest);
                return true;
            }
            case TamerMove.StayHidden:
            {
                character.Motor.Stop();
                return true;
            }
            case TamerMove.HealPet:
            {
                if (TendPet(character, foe, hurt))
                {
                    return true;
                }

                if (!character.Hidden)
                {
                    return false;
                }

                character.Motor.Stop();
                return true;
            }
            case TamerMove.Vanish:
            {
                return TryVanish(character, memory, foe, stance, safeCircle);
            }
            case TamerMove.Hide:
            {
                return HideOut(character);
            }
            case TamerMove.LetPetsLead:
            {
                KeepBand(character, memory, foe, distance, stance, careWantsRoom: false);
                return true;
            }
            default:
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The pet near death is called off: the tamer runs, and the run itself tells the pets "all
    /// follow me" (<see cref="PetOrders.OnActionChanged"/>). It runs on foot: before Lord
    /// Blackthorn's Revenge no pet bonds, and a recall leaves an unbonded pet behind.
    /// </summary>
    private static void SavePet(SosariaCharacter tamer, Memory memory, Mobile foe, BaseCreature pet)
    {
        BreakOff(tamer, memory, foe, ordered: true, FleeSpan());

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} calls its {Pet} off {Foe} at {PetHits}/{PetHitsMax} hits and runs with it",
                tamer.Name,
                pet.GetType().Name,
                foe.Name,
                pet.Hits,
                pet.HitsMax
            );
        }
    }

    /// <summary>A heal or cure on the pet (<see cref="PetKeeper.TendNow"/>). A hidden tamer comes out by it: a cast reveals the caster.</summary>
    private static bool TendPet(SosariaCharacter tamer, Mobile foe, BaseCreature pet)
    {
        var hidden = tamer.Hidden;

        if (!PetKeeper.TendNow(tamer, pet))
        {
            return false;
        }

        if (hidden && LogsOnce(tamer, ComeOutLine, foe))
        {
            logger.Information(
                "{Name} comes out of hiding to heal its {Pet} fighting {Foe}",
                tamer.Name,
                pet.GetType().Name,
                foe.Name
            );
        }

        return true;
    }

    /// <summary>A bandage on the pet beside it, or a heal or cure spell from the spell's reach (<see cref="PetRules.PetSpell"/>).</summary>
    private static bool CanTend(SosariaCharacter tamer, BaseCreature pet) =>
        tamer.InRange(pet, Bandage.Range) && tamer.Backpack?.FindItemByType<Bandage>() != null ||
        tamer.InRange(pet, PetRules.SpellReachTiles) &&
        PetRules.PetSpell(pet.Poisoned, PetRules.Wounded(pet.Hits, pet.HitsMax), tamer.Skills.Magery.Value, tamer.Mana) != null;

    /// <summary>
    /// The engine's Hiding would take now (Hiding.OnUse): the skill is up to it and rested, and
    /// neither the foe nor anything on the tamer has it in sight within the spotting range.
    /// </summary>
    private static bool CanHideFrom(SosariaCharacter character, Memory memory, Mobile foe)
    {
        var hiding = character.Skills.Hiding.Value;

        if (!RecallOutRules.HidesOut(hiding) || Core.TickCount - character.NextSkillTime < 0 || !Unspotted(character, foe, hiding))
        {
            return false;
        }

        for (var i = 0; i < memory.Foes.Count; i++)
        {
            if (memory.Foes[i].AttacksSelf && !Unspotted(character, memory.Foes[i].Mobile, hiding))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Unspotted(SosariaCharacter character, Mobile fighter, double hiding) =>
        fighter?.Deleted != false || fighter.Map != character.Map ||
        TamerTacticsRules.HidesFrom(hiding, NavMetric.Chebyshev(character.Location, fighter.Location), fighter.InLOS(character));
}
