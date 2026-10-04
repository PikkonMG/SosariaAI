using System;
using System.Text.RegularExpressions;

namespace SosariaAI.Configuration;

/// <summary>
/// Lines that speak of doing one job right now: chopping, mining, standing at the anvil,
/// sewing. A persona's idle lines come from its trade, but the person may be at the bank or on
/// the road when one is said: a log buyer at the Britain bank said "lumberjacking is slow going
/// today". Only words for the work in hand count; market talk ("price of ore is up") and a hope
/// ("gm carpenter someday") fit any job. Pure.
/// </summary>
public static class WorkLines
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static readonly (Regex Words, string[] Kinds)[] Jobs =
    [
        (new Regex(@"\b(lumberjacking|chopping|choppin|hatchet|felling)\b", Options), [SkillKinds.Lumberjack]),
        (new Regex(@"\b(mining|minin|pickaxe)\b", Options), [SkillKinds.Mine]),
        (new Regex(@"\b(fishing|fishin|bait|the hooks)\b", Options), [SkillKinds.Fish]),
        (new Regex(@"\b(smithing|smelting|smeltin|hammering|anvil|forge)\b", Options), [SkillKinds.Smith, SkillKinds.Mine]),
        (new Regex(@"\b(sewing|sewin|stitching|needle|loom|tailoring)\b", Options), [SkillKinds.Tailor]),
        (new Regex(@"\b(fletching|fletchin)\b", Options), [SkillKinds.Fletch]),
        (new Regex(@"\b(tinkering|tinkerin)\b", Options), [SkillKinds.Tinker]),
        (new Regex(@"\b(carving|sawing)\b", Options), [SkillKinds.Carpentry]),
        (new Regex(@"\b(cooking|baking|oven)\b", Options), [SkillKinds.Cook]),
        (new Regex(@"\b(brewing|mortar|pestle)\b", Options), [SkillKinds.Alchemy]),
        (new Regex(@"\b(scribing|inscribing)\b", Options), [SkillKinds.Inscription])
    ];

    /// <summary>
    /// True when <paramref name="line"/> speaks of no job, or only of the job the person does
    /// now (<paramref name="doingKind"/>, a <see cref="SkillKinds"/> name, or null when idle).
    /// </summary>
    public static bool FitsWork(string line, string doingKind)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        for (var i = 0; i < Jobs.Length; i++)
        {
            if (Jobs[i].Words.IsMatch(line) && Array.IndexOf(Jobs[i].Kinds, doingKind) < 0)
            {
                return false;
            }
        }

        return true;
    }
}
