using System;
using System.Collections.Generic;
using Server;
using Server.Items;

namespace SosariaAI.Tests;

/// <summary>Short type names in the game content assembly, for checking kit and outfit names.</summary>
internal static class ContentTypes
{
    private static readonly HashSet<string> ItemNames = Collect();

    internal static bool IsItem(string typeName) => typeName != null && ItemNames.Contains(typeName);

    private static HashSet<string> Collect()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in typeof(Katana).Assembly.GetTypes())
        {
            if (!type.IsAbstract && typeof(Item).IsAssignableFrom(type))
            {
                names.Add(type.Name);
            }
        }

        return names;
    }
}
