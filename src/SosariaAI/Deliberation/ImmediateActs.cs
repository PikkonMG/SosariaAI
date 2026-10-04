using System;

namespace SosariaAI.Deliberation;

/// <summary>
/// The acts a decide answer may add beside its routine. Each one is carried out by
/// Brain.ApplyAct: greet says a greeting, go_hunt and go_town switch to the hunt or town
/// routine. Rest and follow were offered once and nothing ran them, while the line that
/// came with them was still spoken; an old answer that names them now reads as no act.
/// </summary>
public static class ImmediateActs
{
    public const string Greet = "greet";
    public const string GoHunt = "go_hunt";
    public const string GoTown = "go_town";

    public static readonly string[] AllowList = [Greet, GoHunt, GoTown];

    public static string Normalize(string act)
    {
        if (string.IsNullOrWhiteSpace(act))
        {
            return null;
        }

        var trimmed = act.Trim();

        for (var i = 0; i < AllowList.Length; i++)
        {
            if (AllowList[i].Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return AllowList[i];
            }
        }

        return null;
    }
}
