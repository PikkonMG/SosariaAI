using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Targets;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// A tamer's orders, said out loud with the engine's pet keywords so the pets' own AI
/// hears and obeys them. "all kill" raises a target cursor, answered on the foe at once.
/// </summary>
public static class PetOrders
{
    private static readonly ILogger logger = SosariaLog.For(typeof(PetOrders));
    private static readonly Dictionary<Serial, (PetCommand Command, DateTime At)> LastCommand = new();

    /// <summary>A fight starts or ends: the pets hear about it at once.</summary>
    public static void OnActionChanged(SosariaCharacter owner) => Direct(owner, force: true);

    /// <summary>
    /// Tells the pets what the moment needs. A new order is said at once; the same order
    /// is said again only after <see cref="PetRules.CommandGap"/>, unless forced.
    /// </summary>
    public static void Direct(SosariaCharacter owner, bool force)
    {
        if (owner is not { Deleted: false, Alive: true } || owner.Map == null || owner.Map == Map.Internal ||
            !PetRules.KeepsPets(owner.PersonProfile.Class))
        {
            return;
        }

        var pets = PetKeeper.PetsOut(owner);

        if (pets.Count == 0)
        {
            return;
        }

        var foe = FoeOf(owner, pets);
        var underGuards = SosariaCharacter.UnderGuards(owner);
        var command = PetRules.Wanted(
            new PetMoment(
                foe != null,
                foe != null && PetRules.MayAttackFoe(underGuards, Notoriety.Compute(owner, foe)),
                AllObey(pets, OrderType.Attack, foe),
                AllObey(pets, OrderType.Guard, null),
                owner.Routine?.CurrentSkill is TameSkill { HoldsPets: true },
                WorldPlay.IsIdle(owner) && underGuards,
                AllObey(pets, OrderType.Follow, owner),
                AllObey(pets, OrderType.Stay, null)
            )
        );

        if (command == PetCommand.None ||
            !force && LastCommand.TryGetValue(owner.Serial, out var last) && last.Command == command &&
            Core.Now - last.At < PetRules.CommandGap)
        {
            return;
        }

        LastCommand[owner.Serial] = (command, Core.Now);
        Give(owner, command, foe);
    }

    /// <summary>Says a line with its keyword so every mobile that listens for it hears the words.</summary>
    public static void Speak(SosariaCharacter speaker, string line, int keyword)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} says: {Line}", speaker.Name, line);
        }

        speaker.DoSpeech(line, [keyword], MessageType.Regular, speaker.SpeechHue);
    }

    private static void Give(SosariaCharacter owner, PetCommand command, Mobile foe)
    {
        switch (command)
        {
            case PetCommand.Kill:
            {
                // A spell cursor already up would swallow the pets' cursor; guarding covers the owner instead.
                if (owner.Target != null)
                {
                    Speak(owner, PetRules.AllGuardMeLine, PetRules.AllGuardMeKeyword);
                    return;
                }

                Speak(owner, PetRules.AllKillLine, PetRules.AllKillKeyword);

                if (owner.Target is AIControlMobileTarget cursor)
                {
                    cursor.Invoke(owner, foe);
                }

                return;
            }
            case PetCommand.Guard:
            {
                Speak(owner, PetRules.AllGuardMeLine, PetRules.AllGuardMeKeyword);
                return;
            }
            case PetCommand.Follow:
            {
                Speak(owner, PetRules.AllFollowMeLine, PetRules.AllFollowMeKeyword);
                return;
            }
            case PetCommand.Stay:
            {
                Speak(owner, PetRules.AllStayLine, PetRules.AllStayKeyword);
                return;
            }
        }
    }

    /// <summary>The owner's live combatant while it fights, never one of its own pets.</summary>
    private static Mobile FoeOf(SosariaCharacter owner, List<BaseCreature> pets)
    {
        if (owner.Motor.Action != CharacterAction.Combat ||
            owner.Combatant is not { Deleted: false, Alive: true } foe ||
            foe.Map != owner.Map || foe is BaseCreature creature && pets.Contains(creature))
        {
            return null;
        }

        return foe;
    }

    private static bool AllObey(List<BaseCreature> pets, OrderType order, Mobile target)
    {
        for (var i = 0; i < pets.Count; i++)
        {
            var pet = pets[i];

            if (pet.ControlOrder != order || target != null && pet.ControlTarget != target)
            {
                return false;
            }
        }

        return true;
    }
}
