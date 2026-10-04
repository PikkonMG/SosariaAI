using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Skills;

namespace SosariaAI.Memory;

/// <summary>
/// When a moment becomes an adventure, and the one plain line that tells it. A human remembers
/// the day's work and the people in it, not every step: wander, loiter and sightseeing are no
/// outing. Pure.
/// </summary>
public static class AdventureRules
{
    /// <summary>A party outing with no fight for this long is over.</summary>
    public static readonly TimeSpan OutingIdleLimit = TimeSpan.FromMinutes(10);

    /// <summary>A heal in a fight between the same two people counts as one rescue this long.</summary>
    public static readonly TimeSpan RescueRest = TimeSpan.FromMinutes(10);

    /// <summary>A heal in a fight is a rescue when the hurt one stood at or under this share of its hits.</summary>
    public const double RescueHitsFraction = 0.5;

    /// <summary>Party members this close to a death were there.</summary>
    public const int PresentTiles = 18;

    /// <summary>A death or a kill counts once.</summary>
    public const int OneDeath = 1;

    public const int OneKill = 1;

    private const int NoKills = 0;
    private const int Once = 1;
    private const int Twice = 2;
    private const string OnceWord = "once";
    private const string TwiceWord = "twice";
    private const string TimesFormat = "{0} times";
    private const string KillsFormat = "{0}: {1} kills";
    private const string OneKillFormat = "{0}: 1 kill";
    private const string NoKillsFormat = "{0}: no kills";
    private const string FellFormat = ", {0} fell {1}";

    /// <summary>A skill people would remember having done that day, as a solo outing.</summary>
    public static bool IsNotableSkill(string skillKind) =>
        skillKind is SkillKinds.Hunt
            or SkillKinds.Mine
            or SkillKinds.Lumberjack
            or SkillKinds.Fish
            or SkillKinds.Dungeon
            or SkillKinds.BankDeposit
            or SkillKinds.VendorSell
            or SkillKinds.House
            or SkillKinds.Tavern
            or SkillKinds.Recall
            or SkillKinds.Steal
            or SkillKinds.Hide
            or SkillKinds.DetectHidden
            or SkillKinds.Lockpick
            or SkillKinds.Tame
            or SkillKinds.Mount
            or SkillKinds.BuyMount
            or SkillKinds.Smith
            or SkillKinds.PlayerVendor
            or SkillKinds.Boat
            or SkillKinds.Mark
            or GhostSkill.SkillName
            or SkillKinds.Flee;

    /// <summary>A party outing inside a dungeon is a dungeon run; anywhere else it is a hunt.</summary>
    public static string OutingKind(bool dungeon) => dungeon ? AdventureKinds.Dungeon : AdventureKinds.Hunt;

    /// <summary>A dungeon run is kept as it is; a hunt only when something died in it.</summary>
    public static bool OutingWorthKeeping(bool dungeon, int kills, int deaths) => dungeon || kills > 0 || deaths > 0;

    /// <summary>True when a heal in a fight saved someone low on hits.</summary>
    public static bool HealIsRescue(bool fighting, int hits, int hitsMax) =>
        fighting && hitsMax > 0 && hits <= hitsMax * RescueHitsFraction;

    /// <summary>True when the same helper has not rescued the same person within <see cref="RescueRest"/>.</summary>
    public static bool RescueRested(DateTime lastRescue, DateTime now) =>
        TimeRules.Rested(lastRescue, now, RescueRest);

    /// <summary>"once", "twice", or "3 times".</summary>
    public static string TimesWord(int times) =>
        times switch
        {
            Once => OnceWord,
            Twice => TwiceWord,
            _ => string.Format(CultureInfo.InvariantCulture, TimesFormat, times)
        };

    /// <summary>A party outing: "Despise: 34 kills, Halvard fell once".</summary>
    public static string PartySummary(string place, int kills, IReadOnlyList<(string Name, int Times)> fallen)
    {
        var text = new StringBuilder();
        var format = kills switch
        {
            NoKills => NoKillsFormat,
            OneKill => OneKillFormat,
            _ => KillsFormat
        };
        text.AppendFormat(CultureInfo.InvariantCulture, format, place, kills);

        for (var i = 0; fallen != null && i < fallen.Count; i++)
        {
            text.AppendFormat(CultureInfo.InvariantCulture, FellFormat, fallen[i].Name, TimesWord(fallen[i].Times));
        }

        return text.ToString();
    }

    /// <summary>A death: by a person or a creature, or by nothing anyone saw.</summary>
    public static string DeathSummary(string fallen, string killer, string place) =>
        string.IsNullOrWhiteSpace(killer) ? $"{fallen} died at {place}" : $"{fallen} fell to {killer} at {place}";

    /// <summary>A red put down: who landed the kill, when anyone did.</summary>
    public static string RedKillSummary(string red, string killer, string place) =>
        string.IsNullOrWhiteSpace(killer) ? $"Killed the red {red} at {place}" : $"{killer} killed the red {red} at {place}";

    /// <summary>A resurrect, or a heal that kept someone up in a fight.</summary>
    public static string RescueSummary(string healer, string helped, bool raised, string place) =>
        raised ? $"{healer} raised {helped} at {place}" : $"{healer} healed {helped} in a fight at {place}";

    public static string DuelSummary(string winner, string beaten, string place) => $"{winner} beat {beaten} in a duel at {place}";

    public static string HouseSummary(string owner, string place) => $"{owner} bought a house near {place}";

    public static string FirstKillSummary(string killer, string victim, string place) =>
        $"{killer} killed a person for the first time: {victim} at {place}";

    /// <summary>A solo outing: "Mine done at Minoc".</summary>
    public static string OutingSummary(string skill, SkillStatus status, string place) =>
        $"{skill} {status.ToString().ToLowerInvariant()} at {place}";
}
