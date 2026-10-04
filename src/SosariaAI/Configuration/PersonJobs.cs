using System;
using SosariaAI.Combat;

namespace SosariaAI.Configuration;

/// <summary>The four jobs a persona can fit. A template's job comes from its build.</summary>
public static class PersonJobs
{
    public const string Worker = "worker";
    public const string Fighter = "fighter";
    public const string Thief = "thief";
    public const string Tamer = "tamer";

    public static readonly string[] All = [Worker, Fighter, Thief, Tamer];

    public static string Of(BuildDefinition build)
    {
        if (string.Equals(build?.Preset, BuildPresets.Thief, StringComparison.OrdinalIgnoreCase))
        {
            return Thief;
        }

        if (string.Equals(build?.Preset, BuildPresets.Tamer, StringComparison.OrdinalIgnoreCase))
        {
            return Tamer;
        }

        if (string.Equals(build?.Role, "worker", StringComparison.OrdinalIgnoreCase))
        {
            return Worker;
        }

        return Fighter;
    }

    public static bool Fits(Persona persona, string job)
    {
        if (persona?.Jobs == null)
        {
            return false;
        }

        for (var i = 0; i < persona.Jobs.Count; i++)
        {
            if (string.Equals(persona.Jobs[i], job, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
