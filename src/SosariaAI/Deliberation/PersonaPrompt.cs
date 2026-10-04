using System.Collections.Generic;
using System.Text;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Spawning;

namespace SosariaAI.Deliberation;

/// <summary>Who a copy is, as the persona writer tells the model.</summary>
public sealed record PersonaFacts(
    string Name,
    bool Female,
    PersonClass Class,
    SkillTier Tier,
    PersonTrait Traits,
    PersonWealth Wealth,
    string HomeTown,
    string GuildTag,
    EraBand Band
);

/// <summary>
/// The one-time request that asks the chat model for a copy's own persona. The system
/// message holds the 1999 chat rules and the exact JSON shape; the user message holds the
/// person. <see cref="PersonaDraftRules"/> checks the answer.
/// </summary>
public static class PersonaPrompt
{
    /// <summary>A persona is about 40 short lines. This leaves room for them and the JSON around them.</summary>
    public const int MaxReplyTokens = 1600;

    /// <summary>
    /// Lines asked over each kept count of the longer lists. <see cref="PersonaDraftRules.Vet"/>
    /// drops a bad or promise line and takes the next one, so a few spares keep a paid draft
    /// from failing short.
    /// </summary>
    public const int SpareLines = 2;

    /// <summary>Lines asked over each kept count of the shorter lists: return, combat and loot.</summary>
    public const int SpareFewLines = 1;

    public static readonly string JsonShape =
        $"{{\"background\":\"1-2 sentences\",\"voice\":\"how you type\",\"likes\":[{PersonaDraftRules.LikeCount} items]," +
        $"\"dislikes\":[{PersonaDraftRules.DislikeCount} items],\"wants\":[{PersonaDraftRules.WantCount} items]," +
        $"\"idleLines\":[{PersonaDraftRules.IdleCount + SpareLines} lines]," +
        $"\"greetingLines\":[{PersonaDraftRules.GreetingCount + SpareLines} lines]," +
        $"\"returnLines\":[{PersonaDraftRules.ReturnCount + SpareFewLines} lines]," +
        $"\"combatLines\":[{PersonaDraftRules.CombatCount + SpareFewLines} lines]," +
        $"\"lootLines\":[{PersonaDraftRules.LootCount + SpareFewLines} lines]}}";

    /// <summary>
    /// No written line may promise a group, a trip or a meeting. Written lines are said at
    /// random to whoever stands near, and nothing makes the person keep the promise: a red
    /// camping Destard asked a player along to Minoc and never left.
    /// </summary>
    public const string NoInviteRule =
        "no line invites anyone to come along, follow, join, group up or meet, and no line says the person " +
        "or the listener is heading or going somewhere, because these lines are said at random times " +
        "to whoever stands near and the person never goes";

    private const string Unknown = "none";
    private const string ListSeparator = ", ";

    public static bool IsPersonaWrite(BrainEventKind kind) => kind == BrainEventKind.PersonaWrite;

    public static string SystemMessage(EraBand band)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You write one person who lives in Sosaria, in Ultima Online as it was played in the era below.");
        builder.AppendLine("Everything you write is what this one person says in chat, or a short note about them.");
        builder.AppendLine("Chat rules for every line:");
        builder.AppendLine("- type like a real player in 1999: mostly lowercase, short, under 60 characters");
        builder.AppendLine("- plain words and era slang where it fits: hail, lol, heh, brb, afk, wts, wtb, gz, ty, np, rez, pk, lag, newb, vendor");
        builder.AppendLine("- no roleplay emotes, no asterisks, no quotes, no emoji, no dashes, plain ASCII only");
        builder.AppendLine("- every line different from every other line; no line says the person's own name");
        builder.AppendLine("- greeting lines may use {name} where the other person's name goes");
        builder.AppendLine("- never mention being an AI, a bot, a program, or a game, or anything from outside Sosaria");
        builder.Append("- ");
        builder.AppendLine(NoInviteRule);
        builder.Append("Era: ");
        builder.AppendLine(EraText(band));

        var notYet = PersonaEraWords.NotYetIn(band);

        if (notYet.Count > 0)
        {
            builder.Append("These do not exist yet in this era; never name them: ");
            builder.AppendLine(string.Join(ListSeparator, notYet));
        }

        builder.AppendLine("background: where they came from and what they do, 1-2 sentences, third person.");
        builder.AppendLine("wants: two long hopes for some later day, each a short phrase; never a plan for today.");
        builder.AppendLine("idleLines: things said aloud while working or standing about. returnLines: said after coming back from death.");
        builder.AppendLine("combatLines: said when a fight starts. lootLines: said over a good find.");
        builder.Append("Reply ONLY with one JSON object of this exact shape and nothing else: ");
        builder.Append(JsonShape);
        return builder.ToString();
    }

    public static string UserMessage(PersonaFacts facts)
    {
        var builder = new StringBuilder();
        builder.Append("Name: ");
        builder.AppendLine(facts.Name);
        builder.Append("Gender: ");
        builder.AppendLine(facts.Female ? "female" : "male");
        builder.Append("Class: ");
        builder.Append(facts.Tier.ToString().ToLowerInvariant());
        builder.Append(' ');
        builder.AppendLine(PersonClassRules.Title(facts.Class).ToLowerInvariant());
        builder.Append("Temper: ");
        builder.AppendLine(Words(IdentityLine.TraitWords(facts.Traits)));
        builder.Append("Purse: ");
        builder.AppendLine(IdentityLine.WealthWord(facts.Wealth));
        builder.Append("Home town: ");
        builder.AppendLine(string.IsNullOrWhiteSpace(facts.HomeTown) ? Unknown : facts.HomeTown);
        builder.Append("Guild tag: ");
        builder.AppendLine(string.IsNullOrWhiteSpace(facts.GuildTag) ? Unknown : $"[{facts.GuildTag}]");
        builder.Append("Write this person.");
        return builder.ToString();
    }

    public static string EraText(EraBand band) =>
        band switch
        {
            EraBand.T2A => "1998 to 2002, The Second Age to Lord Blackthorn's Revenge",
            EraBand.ML => "2003 to 2007, Age of Shadows, Samurai Empire and Mondain's Legacy",
            _ => "2009 on, Stygian Abyss and later"
        };

    private static string Words(List<string> words) =>
        words.Count == 0 ? Unknown : string.Join(ListSeparator, words);
}
