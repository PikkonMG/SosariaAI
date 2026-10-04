using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Notices a person arriving on a dungeon floor, by a door, a stair, a gate or a recall,
/// and writes one line per arrival: "{Name} entered {Dungeon} level {N}". Moving round one
/// floor writes nothing; stepping down a stair writes the new level.
/// </summary>
public static class DungeonGate
{
    private static readonly ILogger logger = SosariaLog.For(typeof(DungeonGate));
    private static readonly Dictionary<Serial, (string Dungeon, int Level)> Where = new();

    public static string EntryLine(string name, string dungeon, int level) =>
        $"{name} entered {dungeon} level {level}";

    /// <summary>True when the person now stands on a dungeon level it was not on before.</summary>
    public static bool IsNewArrival((string Dungeon, int Level)? before, DungeonFloor? now) =>
        now is { } floor &&
        (before is not { } was ||
         was.Level != floor.Level ||
         !string.Equals(was.Dungeon, floor.Dungeon, StringComparison.OrdinalIgnoreCase));

    /// <summary>The floor the person stands on now, noted and logged when it is a new arrival.</summary>
    public static DungeonFloor? NoteWhere(SosariaCharacter character)
    {
        if (!People.InWorld(character))
        {
            return null;
        }

        var floor = DungeonAtlas.For(character.Map.Name).FloorAt(character.Location);
        (string, int)? before = Where.TryGetValue(character.Serial, out var was) ? was : null;

        if (floor is not { } now)
        {
            Where.Remove(character.Serial);
            return null;
        }

        if (IsNewArrival(before, now) && SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", EntryLine(character.Name, now.Dungeon, now.Level));
        }

        Where[character.Serial] = (now.Dungeon, now.Level);
        return now;
    }
}
