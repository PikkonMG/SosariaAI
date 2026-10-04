using System.Text.Json;
using SosariaAI.Configuration;

namespace SosariaAI.Tests;

/// <summary>A persona draft that passes every check, for tests to break one piece at a time.</summary>
internal static class PersonaDraftSamples
{
    public const string CopyId = "Felucca:connor#7";

    public static PersonaDraft Valid() =>
        new()
        {
            Background = "Came up from Vesper with a borrowed pick. Mines the north caves and sells ore at the Minoc forge.",
            Voice = "types fast in lower case, short lines, says heh a lot",
            Likes = ["cold ale", "a full pack of iron", "quiet caves"],
            Dislikes = ["ore thieves", "lag", "reds at the mine"],
            Wants = ["save for a small house near Minoc.", "reach grandmaster mining"],
            IdleLines =
            [
                "anyone need iron",
                "this vein is rich heh",
                "pick broke again",
                "gonna smelt soon",
                "ore prices r low today",
                "seen any reds up north",
                "my back hurts from this",
                "need a new shovel",
                "minoc is busy tonight",
                "brb banking",
                "who wants valorite",
                "slow day in the caves"
            ],
            GreetingLines =
            [
                "hail {name}",
                "hey {name}",
                "yo {name} whats up",
                "sup {name}",
                "{name}! good to see ya",
                "evening {name}"
            ],
            ReturnLines = ["that hurt", "back from the dead again", "lost my ore to that thing"],
            CombatLines = ["get off me", "die already", "guards!", "run for it"],
            LootLines = ["nice drop", "gold gold gold", "heh got some gems"]
        };

    public static string ModelReply(PersonaDraft draft) => JsonSerializer.Serialize(draft);
}
