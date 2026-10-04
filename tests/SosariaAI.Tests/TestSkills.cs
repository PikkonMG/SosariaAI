using System;
using System.Linq;
using Server;

namespace SosariaAI.Tests;

/// <summary>
/// The server reads its skill table from its data folder. Without one a mobile has no skill
/// slots and every skill read is null; tests that read skills need only the slots.
/// </summary>
internal static class TestSkills
{
    private static readonly object Gate = new();

    internal static void EnsureTable()
    {
        lock (Gate)
        {
            if (SkillInfo.Table.Length != 0)
            {
                return;
            }

            SkillInfo.Table = Enum.GetValues<SkillName>()
                .Select(skill => new SkillInfo(
                    (int)skill, skill.ToString(), 0, 0, 0, skill.ToString(), null, 0, 0, 0, 1, null, Stat.Str, Stat.Dex
                ))
                .ToArray();
        }
    }
}
