using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace SosariaAI.Spawning;

/// <summary>
/// Names worn on the shard, so a copy never takes a name another person wears. Built
/// once from the loaded world, then kept by every name the plugin hands out. An entry
/// is checked against the live mobile before it counts, so a deleted or renamed
/// person frees its old name without any hook. Fixture names are held for their
/// fixtures even before those spawn. Main thread only.
/// </summary>
public static class NameRegistry
{
    private static readonly Dictionary<string, Serial> Worn = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    /// <summary>Holds a fixture's authored name so no copy takes it first.</summary>
    public static void Reserve(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Reserved.Add(name);
        }
    }

    /// <summary>True when another live player or a fixture holds this name.</summary>
    public static bool IsTaken(string name, Mobile self)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        LoadOnce();

        if (Reserved.Contains(name))
        {
            return true;
        }

        if (!Worn.TryGetValue(name, out var serial))
        {
            return false;
        }

        var holder = World.FindMobile(serial);

        if (holder is { Deleted: false } && string.Equals(holder.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            return !ReferenceEquals(holder, self);
        }

        Worn.Remove(name);
        return false;
    }

    public static void Claim(string name, Mobile owner)
    {
        if (!string.IsNullOrWhiteSpace(name) && owner != null)
        {
            LoadOnce();
            Worn[name] = owner.Serial;
        }
    }

    private static void LoadOnce()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is PlayerMobile { Deleted: false } player && !string.IsNullOrWhiteSpace(player.Name))
            {
                Worn.TryAdd(player.Name, player.Serial);
            }
        }
    }
}
