using System;
using Server;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Economy;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Logging;

namespace SosariaAI.Mobiles;

public static class RoutineDriver
{
    private static readonly ILogger logger = SosariaLog.For(typeof(RoutineDriver));

    // A character that asked a player to party listens for the answer even with the Brain off.
    public static bool HandlesOnSpeech(SosariaCharacter character, Mobile from)
    {
        if (character == null || from == null || from == character)
        {
            return false;
        }

        return AwaitsAnswerFrom(character, from) || SpeechResponder.Hears(character, from);
    }

    public static void OnSpeech(SosariaCharacter character, SpeechEventArgs e)
    {
        if (!People.IsHuman(e?.Mobile) || string.IsNullOrEmpty(e.Speech))
        {
            return;
        }

        if (GameParty.NoteSpokenAnswer(character, e.Mobile, e.Speech))
        {
            return;
        }

        SpeechResponder.Hear(character, e.Mobile, e.Speech);
    }

    private static bool AwaitsAnswerFrom(SosariaCharacter character, Mobile from) =>
        character.InviteTarget == from.Serial;

    /// <summary>Runs once each time <see cref="CharacterMotor.Action"/> changes.</summary>
    public static void OnActionChanged(SosariaCharacter character)
    {
        if (character == null)
        {
            return;
        }

        var action = character.Motor.Action;
        PetOrders.OnActionChanged(character);

        if (character.IsGhost || character.Routine?.CurrentSkill is GhostSkill)
        {
            return;
        }

        if (action is CharacterAction.Combat or CharacterAction.Flee)
        {
            character.Conversation.Clear();
        }

        if (action == CharacterAction.Combat)
        {
            character.FoughtThisStep = true;
            character.TrySayCombatLine();
        }

        // A fight is run, not walked: a mage backing off at a walk is caught by anything.
        if (action is CharacterAction.Combat or CharacterAction.Flee)
        {
            character.SetRunPace();
        }

        if (IsHunting(character) || character.Routine?.CurrentSkill is FleeSkill)
        {
            return;
        }

        var actionIsWander = action == CharacterAction.Wander;

        if (action == CharacterAction.Combat)
        {
            HoldForFight(character);
        }
        else if (FleeRules.ShouldAbortForAiAction(character.Routine?.CurrentSkill?.Name, actionIsWander))
        {
            character.Routine?.AbortActive();
        }
        else if (actionIsWander)
        {
            character.RestoreTownStance();
        }
    }

    /// <summary>
    /// A fight holds the job instead of ending it: the walk home or the errand goes on from
    /// where it stood once the fight is over (<see cref="Wander"/> resumes it), with no new
    /// decision. Ending it made one anti-PK start GoHome 26 times and finish it never. A hunt
    /// runs through its own fights and is not held.
    /// </summary>
    public static void HoldForFight(SosariaCharacter character)
    {
        if (character?.Routine is { } routine && routine.CurrentSkill is not IHuntingSkill)
        {
            routine.Pause(Core.Now);
        }
    }

    /// <summary>
    /// A call into someone else's fight: a rally, a watch, a draft. A hunt picks its own prey
    /// and would turn the fight back to it, so it ends; any other job waits. A dungeon trip is
    /// the whole run, not one hunt: one blow on a stranger ended every nearby crawler's trip.
    /// Its room hunt fights through the call, and between rooms the trip waits like a job.
    /// </summary>
    public static void HoldForCalledFight(SosariaCharacter character)
    {
        switch (character?.Routine?.CurrentSkill)
        {
            case DungeonTripSkill { IsHunting: true }:
                return;
            case DungeonTripSkill:
                character.Routine.Pause(Core.Now);
                return;
            case IHuntingSkill:
                character.Routine.AbortActive();
                return;
            default:
                HoldForFight(character);
                return;
        }
    }

    private static void ConsiderDanger(SosariaCharacter character)
    {
        // A tamer on a taming trip walks up to dragons on purpose; it gives up by its own rule.
        if (character.IsGhost || character.Routine?.CurrentSkill is GhostSkill or FleeSkill || TameSkill.BravesDanger(character))
        {
            return;
        }

        var threat = HuntSkill.SightThreat(character);
        var guarded = SosariaCharacter.UnderGuards(character);

        if (!FleeRules.MayFlee(guarded, threat))
        {
            return;
        }

        var role = character.Build?.Role ?? CharacterRole.Worker;

        if (character.Routine?.CurrentSkill is IHuntingSkill &&
            !DangerRules.MustFlee(role, threat))
        {
            return;
        }

        if (FleeRules.ShouldAbortWork(
                character.MustRunFrom(threat),
                FleeRules.FleeIsBlocked(character.FailedRoutineId, character.FailedRoutineCount),
                character.Routine?.CurrentSkill?.Name))
        {
            TownTrip.GiveUpForThreat(character, Core.Now);
        }
    }

    // The character stands and faces the player it is talking to. The routine waits.
    private static void HoldForConversation(SosariaCharacter character)
    {
        character.Motor.ClearMoveIntent();

        if (World.FindMobile(character.Conversation.Partner) is not { Deleted: false } partner ||
            partner.Map != character.Map ||
            !character.InRange(partner, Brain.ReplyHearRange))
        {
            character.Conversation.Clear();
            return;
        }

        character.Direction = character.GetDirectionTo(partner);
    }

    /// <summary>
    /// One decision tick from <see cref="CharacterPulse"/>. A fight or a flee belongs to the
    /// combat brain; otherwise the routine runs. The world scan runs on its own pace.
    /// </summary>
    public static void Think(SosariaCharacter character)
    {
        if (character == null || character.Deleted)
        {
            return;
        }

        var motor = character.Motor;

        if (character.IsGhost)
        {
            character.Routine?.Tick();
            return;
        }

        if (!motor.StepOffTrap())
        {
            motor.StepOutOfDoorway();
        }

        if (ShouldFightBack(character))
        {
            EnterFight(character);
        }

        if (motor.Action is CharacterAction.Combat or CharacterAction.Flee)
        {
            CombatBrain.Think(character);

            if (IsHunting(character) && motor.Action != CharacterAction.Wander)
            {
                character.Routine?.Tick();
            }
        }
        else
        {
            Wander(character);
        }

        if (character.WorldScanDue())
        {
            WorldPlay.Tick(character);
        }
    }

    private static bool ShouldFightBack(SosariaCharacter character) =>
        DefendRules.ShouldFightBack(
            character.Alive,
            character.IsGhost,
            character.Combatant is { Deleted: false, Alive: true },
            character.Combatant?.Map == character.Map,
            character.Motor.Action == CharacterAction.Flee
        );

    private static void EnterFight(SosariaCharacter character)
    {
        GearEquip.EquipReadyWeapon(character);
        character.Warmode = true;
        character.FightMode = FightMode.Evil;
        character.Motor.Action = CharacterAction.Combat;
        GuardCall.TryCall(character, character.Combatant);
    }

    private static void Wander(SosariaCharacter character)
    {
        if (IsHunting(character) && HuntSkill.TryAcquire(character, () => CombatBrain.AcquireFocus(character)))
        {
            character.Combatant = character.FocusMob;
            character.Motor.Action = CharacterAction.Combat;
            return;
        }

        if (character.Conversation.GiveUpIfDue(Core.Now) && SosariaSettings.LogActivity)
        {
            logger.Information("{Name} gave up waiting for a reply", character.Name);
        }

        if (character.Conversation.IsActive(Core.Now))
        {
            character.Routine?.Pause(Core.Now);
            HoldForConversation(character);
            return;
        }

        character.Routine?.Resume(Core.Now);

        var watched = PresenceFocus.MustThinkFast(
            PresenceFocus.ClientWatching(character),
            character.Combatant is { Deleted: false, Alive: true },
            character.IsGhost,
            IsHunting(character)
        );
        character.ApplyPresencePace(watched);

        if (character.DangerScanDue(watched))
        {
            ConsiderDanger(character);
        }

        CombatBrain.TendWounds(character);

        // An overloaded body stands still between steps, whatever the job: the surplus goes first.
        CarryLoad.Shed(character);

        var routine = character.Routine;

        if (routine == null || routine.NeedsNext)
        {
            character.ScoreAndCommit();
            routine = character.Routine;
        }

        if (routine == null)
        {
            character.Motor.LoiterInHome(IdleWanderSkill.WanderChanceToNotMove);
        }
        else
        {
            routine.Tick();

            if (routine.NeedsNext)
            {
                character.ScoreAndCommit();
            }
        }

        if (watched && character.AmbientScanDue())
        {
            Meeting.Consider(character);
            Brain.NoticeNearbyPlayer(character);
            character.NoticeLifeForMusing();
        }
    }

    public static void OnAggressiveAction(SosariaCharacter character, Mobile aggressor)
    {
        if (character == null || aggressor == null || !People.Perceives(character, aggressor))
        {
            return;
        }

        if (character.IsGhost || character.Routine?.CurrentSkill is GhostSkill)
        {
            return;
        }

        GuardCall.TryCall(character, aggressor);
        GearEquip.EquipReadyWeapon(character);
        var helpers = Assist.Rally(character, aggressor);
        var armed = GearScore.HasWeapon(character);
        var pvp = People.IsLivingPlayer(aggressor);

        // The sight scan is blind under guards and was blind to war enemies at all.
        // Whoever is already landing the hits always counts toward the threat call.
        var threat = Math.Max(
            HuntSkill.SightThreat(character),
            ThreatRating.Score([HostileRead.Of(aggressor)])
        );

        // A hit that lands during a flee means the attacker is still in reach: the flee
        // opened no distance. CheckFlee also clears a timer that already ran out.
        var motor = character.Motor;
        var fleeing = character.CheckFlee() || motor.Action == CharacterAction.Flee;
        var fightingThisAttacker = motor.Action == CharacterAction.Combat && character.Combatant == aggressor;
        var mayFlee = FleeRules.MayAnswerHitWithFlee(fleeing, fightingThisAttacker);

        if (mayFlee && DefendRules.RunsBare(pvp, SpareKit.Armed(character)) &&
            !FleeRules.FleeIsBlocked(character.FailedRoutineId, character.FailedRoutineCount))
        {
            character.Combatant = aggressor;
            CombatBrain.RunFrom(character, aggressor);
            return;
        }

        if (!pvp &&
            mayFlee && character.Build?.Role == CharacterRole.Worker &&
            !AssistRules.ShouldStandAndFight(armed, helpers) &&
            FleeRules.MayFleeAttacker(threat) &&
            !FleeRules.FleeIsBlocked(character.FailedRoutineId, character.FailedRoutineCount))
        {
            character.Routine?.AbortActive();
            character.Combatant = aggressor;
            character.Warmode = false;
            CombatBrain.RunFrom(character, aggressor);
            return;
        }

        // A hunt chose its fights by its dare, so it answers an attacker unless the attacker
        // is far past that dare; any other job runs from what it would not fight back.
        if (!pvp &&
            mayFlee && character.Build?.Role != CharacterRole.Worker &&
            (character.Routine?.CurrentSkill is IHuntingSkill
                ? CombatBrain.Overwhelms(character, aggressor)
                : !DangerRules.ShouldFight(
                    CharacterPower.For(character),
                    threat,
                    Vitals.HitsFraction(character),
                    hasHealing: character.Build?.CanHeal == true,
                    Party.AlliesPower(character),
                    SosariaSettings.Characters?.Career?.ThreatMultiple ?? ThreatRating.DefaultThreatMultiple
                )))
        {
            character.Combatant = aggressor;
            CombatBrain.RunFrom(character, aggressor);
            return;
        }

        // A retreat takes hits on the way out, from a person as from a pack. Turning back on
        // a person here flipped the body between flee and fight every think; cornered or
        // barely hurt, the combat brain stands the runner at bay.
        if (fleeing)
        {
            return;
        }

        HoldForFight(character);

        var current = character.Combatant;

        // One foe at a time: a new attacker takes the fight only when it is clearly nearer.
        if (current == null ||
            current != aggressor &&
            FocusRules.ClearlyNearer(
                NavMetric.Chebyshev(character.Location, aggressor.Location),
                NavMetric.Chebyshev(character.Location, current.Location),
                current.Combatant == character
            ))
        {
            character.Combatant = aggressor;
        }

        character.StopFlee();
        character.Warmode = true;
        character.FightMode = FightMode.Evil;
        motor.Action = CharacterAction.Combat;
        character.Pulse.WakeNow();
    }

    public static bool IsHunting(SosariaCharacter character) =>
        character?.IsHunting == true || character?.Routine?.CurrentSkill is IHuntingSkill { IsHunting: true };
}
