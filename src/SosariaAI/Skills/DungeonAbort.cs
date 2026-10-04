using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Writes the end of a dungeon trip that another job took over: "{Name} ended Dungeon
/// (Aborted: {reason}) at {Location}". A trip broken off by a death, a party call or a friend
/// to raise wrote no end line at all: 249 runs started in twenty minutes and 34 ended, and the
/// rest could not be told from the runs still on the road. The trip notes its own abort; the
/// start of the job that took over names the reason and writes the line.
/// </summary>
public static class DungeonAbort
{
    public const string Status = "Aborted";
    public const string RaiseWhy = "it stopped to raise a fallen friend";
    public const string NewRunWhy = "a new dungeon run took its place";
    public const string PartyWhy = "it went to join its party";
    public const string FleeWhy = "it fled a threat";
    public const string TookOverWhy = "took over";

    private static readonly ILogger logger = SosariaLog.For(typeof(DungeonAbort));
    private static readonly HashSet<Serial> Pending = [];

    /// <summary>Why the trip ended, read from the job that took its place.</summary>
    public static string Reason(string nextSkill) =>
        nextSkill switch
        {
            GhostSkill.SkillName => DelveFailure.Died,
            ResurrectAidSkill.SkillName => RaiseWhy,
            SkillKinds.Dungeon => NewRunWhy,
            SkillKinds.Follow or PartyGatherSkill.SkillName => PartyWhy,
            SkillKinds.Flee => FleeWhy,
            _ => $"{nextSkill} {TookOverWhy}"
        };

    public static string Line(string name, string reason, Point3D at) =>
        $"{name} ended {SkillKinds.Dungeon} ({Status}: {reason}) at {at}";

    /// <summary>The trip was broken off while it ran: the next job's start writes its end.</summary>
    public static void Note(SosariaCharacter character)
    {
        if (character != null)
        {
            Pending.Add(character.Serial);
        }
    }

    /// <summary>A job started: a trip it broke off gets its end line first.</summary>
    public static void OnNextStarted(SosariaCharacter character, string nextSkill)
    {
        if (character == null || !Pending.Remove(character.Serial) || !SosariaSettings.LogActivity)
        {
            return;
        }

        logger.Information("{Line}", Line(character.Name, Reason(nextSkill), character.Location));
    }
}
