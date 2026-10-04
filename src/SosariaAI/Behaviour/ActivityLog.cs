using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;
using SosariaAI.Logging;

namespace SosariaAI.Behaviour;

public static class ActivityLog
{
    private static readonly ILogger logger = SosariaLog.For(typeof(ActivityLog));
    private static readonly Dictionary<Serial, DateTime> LastFailureLog = [];

    public static void Subscribe(Routine routine)
    {
        routine.SkillStarted += OnStarted;
        routine.SkillEnded += OnEnded;
    }

    private static void OnStarted(SosariaCharacter character, Skill skill)
    {
        DungeonAbort.OnNextStarted(character, skill.Name);

        if (!SosariaSettings.LogActivity || character == null || !Allow(character, isFailure: false))
        {
            return;
        }

        logger.Information("{Name} started {Skill} at {Location}", character.Name, skill.Name, character.Location);
    }

    private static void OnEnded(SosariaCharacter character, Skill skill, SkillStatus status)
    {
        if (character == null)
        {
            return;
        }

        AdventureTracker.Shared.SkillEnded(character, skill.Name, status, Core.Now);

        if (status == SkillStatus.Failed)
        {
            ActivityPulse.NoteFailed();
        }
        else
        {
            ActivityPulse.NoteDone();
        }

        if (!SosariaSettings.LogActivity)
        {
            return;
        }

        if (!Allow(character, status == SkillStatus.Failed))
        {
            return;
        }

        if (status == SkillStatus.Failed && !string.IsNullOrWhiteSpace(skill.FailReason))
        {
            logger.Information(
                "{Name} ended {Skill} ({Status}: {Reason}) at {Location}",
                character.Name,
                skill.Name,
                status,
                skill.FailReason,
                character.Location
            );
        }
        else
        {
            logger.Information(
                "{Name} ended {Skill} ({Status}) at {Location}",
                character.Name,
                skill.Name,
                status,
                character.Location
            );
        }

        if (skill is LumberjackSkill lumberjack)
        {
            logger.Information(
                "{Name} backpack holds {Count} logs after Lumberjack",
                character.Name,
                lumberjack.ResourceCount
            );
        }
        else if (skill is MineSkill mine)
        {
            logger.Information(
                "{Name} backpack holds {Count} ore after Mine",
                character.Name,
                mine.ResourceCount
            );
        }
        else if (skill is FishSkill fish)
        {
            logger.Information(
                "{Name} backpack holds {Count} fish after Fish",
                character.Name,
                fish.ResourceCount
            );
        }
        else if (skill is BankDepositSkill bank)
        {
            logger.Information(
                "{Name} deposited {Count} items at the bank",
                character.Name,
                bank.ItemsDeposited
            );
        }
    }

    // A step that fails over and over writes three lines per cycle: the start, the failure
    // and the routine restart. The first failure is logged, then the character stays quiet
    // for one window. Any step that does not fail ends the window at once, so an isolated
    // failure never hides a character's real work.
    private static bool Allow(SosariaCharacter character, bool isFailure)
    {
        var now = Core.Now;
        LastFailureLog.TryGetValue(character.Serial, out var lastFailure);

        if (!isFailure && !EndsQuietWindow(character, lastFailure))
        {
            return !ActivityLogLimit.InQuietWindow(lastFailure, now);
        }

        if (!isFailure)
        {
            return true;
        }

        if (!ActivityLogLimit.AllowFailureLog(lastFailure, now))
        {
            return false;
        }

        LastFailureLog[character.Serial] = now;
        return true;
    }

    /// <summary>A step that ended without failing clears the quiet window for that character.</summary>
    private static bool EndsQuietWindow(SosariaCharacter character, DateTime lastFailure) =>
        lastFailure != default && LastFailureLog.Remove(character.Serial);
}
