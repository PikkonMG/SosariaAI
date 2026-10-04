using System;
using System.Collections.Generic;

namespace SosariaAI.Social;

/// <summary>Pure rules for characters recruited into a guild a player founded.</summary>
public static class GuildRecruitRules
{
    public static bool IsPlayerGuildTag(string tag, IReadOnlyList<GuildRecord> pluginGuilds)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        for (var i = 0; i < pluginGuilds.Count; i++)
        {
            if (string.Equals(pluginGuilds[i].Tag, tag, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static string JoinLine(string tag) =>
        string.IsNullOrWhiteSpace(tag) ? "glad to be in" : $"{tag.ToLowerInvariant()} for life";
}
