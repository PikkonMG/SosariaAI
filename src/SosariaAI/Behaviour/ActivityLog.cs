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

        if (!SosariaSettings.LogActivity || character == null || !Allow(character, StepLine.Started))
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

        if (!Allow(character, status == SkillStatus.Failed ? StepLine.Failed : StepLine.Ended))
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

        if (skill is HarvestSkill harvest)
        {
            logger.Information(
                "{Name} gathered {Gained} {Haul} on {Skill} in {SwingTries} swing tries at skill {WorkSkill:F1}; the backpack holds {Count}",
                character.Name,
                harvest.Gained,
                harvest.HaulNoun,
                skill.Name,
                harvest.SwingTries,
                harvest.WorkSkill,
                harvest.ResourceCount
            );
            LogEngineMessage(character, harvest);
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

    /// <summary>
    /// A trip that gathered nothing says what the engine told the worker last, the cliloc
    /// number or the plain text (<see cref="HarvestSkill.EngineMessage"/>). Woodcutters ended
    /// 163 trips of one night with nothing cut and no word of why: their walks stopped a tile
    /// past the engine's reach, where it refuses a swing "That is too far away" (500446), and a
    /// worker has no client to read that.
    /// </summary>
    private static void LogEngineMessage(SosariaCharacter character, HarvestSkill harvest)
    {
        if (harvest.Gained > 0 || HarvestSkill.MessageText(harvest.EngineMessage) is not { } message)
        {
            return;
        }

        logger.Information(
            "{Name} gathered nothing on {Skill}; last engine message: {Message}",
            character.Name,
            harvest.Name,
            message
        );
    }

    // A step that fails over and over writes three lines per cycle: the start, the failure
    // and the routine restart. The rules in ActivityLogLimit.Next keep the cycle quiet for one
    // window after a logged failure; a step that ends without failing ends the window at once.
    private static bool Allow(SosariaCharacter character, StepLine line)
    {
        var (write, lastFailure) = ActivityLogLimit.Next(line, LastFailureLog.GetValueOrDefault(character.Serial), Core.Now);

        if (lastFailure == default)
        {
            LastFailureLog.Remove(character.Serial);
        }
        else
        {
            LastFailureLog[character.Serial] = lastFailure;
        }

        return write;
    }
}
