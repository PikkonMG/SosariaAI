using System;
using SosariaAI.Configuration;

namespace SosariaAI.Economy;

/// <summary>
/// Who answers a shout on a bank floor. A WTS or a WTB a character shouted waited for a buyer or
/// seller who had come to the bank to shop that same minute, and one bank shout in eleven ended
/// in a deal (43 deals to 519 bank shopping trips in a run). On a live shard the person banking
/// or standing about answered: "what u want for the kryss?". Only a person idling on the floor
/// answers, never one on its way somewhere, and a shouter looks for an answer a beat at a time,
/// not every think. Pure.
/// </summary>
public static class FloorTradeRules
{
    /// <summary>Seconds between one shouter's looks round the floor for someone who answers.</summary>
    public const int AnswerGapSeconds = 15;

    public static readonly TimeSpan AnswerGap = TimeSpan.FromSeconds(AnswerGapSeconds);

    /// <summary>
    /// True for a job that leaves a person standing about with time for a deal: banking, the
    /// bank crowd, loitering, resting, a drink, practice at the bank. A shopper at the bank
    /// shops the hawkers itself, and a walk to anywhere else is not interrupted.
    /// </summary>
    public static bool IsIdleOnFloor(string skillKind) =>
        skillKind is SkillKinds.BankDeposit or SkillKinds.BankCrowd or SkillKinds.Loiter or SkillKinds.IdleWander
            or SkillKinds.Rest or SkillKinds.Arrive or SkillKinds.Decide or SkillKinds.Practice or SkillKinds.Tavern
            or SkillKinds.Taste or SkillKinds.ItemId or SkillKinds.ArmsLore or SkillKinds.Forensic
            or SkillKinds.Meditate or SkillKinds.Music;

    /// <summary>True when the shouter's next look round the floor is due.</summary>
    public static bool LookDue(DateTime now, DateTime lastLook) => now - lastLook >= AnswerGap;
}
