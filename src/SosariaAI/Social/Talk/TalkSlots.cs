using System;
using System.Collections.Generic;
using System.Globalization;

namespace SosariaAI.Social;

/// <summary>A slot a talk line may name, written in a talk file as {name}, {foe} and so on.</summary>
[Flags]
public enum TalkSlot
{
    None = 0,

    /// <summary>The person spoken to.</summary>
    Name = 1 << 0,

    /// <summary>The person the line is about: the one who died, the other duelist.</summary>
    Target = 1 << 1,

    /// <summary>A named spot: a hunting ground, a region.</summary>
    Place = 1 << 2,

    /// <summary>Goods, as people say them: "gm katana", "lvl 3 tmap", "ore".</summary>
    Item = 1 << 3,

    /// <summary>Gold said the 1999 way: "4k", "750".</summary>
    Price = 1 << 4,

    /// <summary>The town the speaker stands in.</summary>
    Town = 1 << 5,

    /// <summary>The dungeon the speaker is in or bound for.</summary>
    Dungeon = 1 << 6,

    /// <summary>The monster or person fought, without its article.</summary>
    Foe = 1 << 7,

    /// <summary>A number of things.</summary>
    Count = 1 << 8,

    /// <summary>A guild's name or tag.</summary>
    Guild = 1 << 9,

    /// <summary>What the speaker is busy with: "mining at minoc".</summary>
    Doing = 1 << 10,

    /// <summary>An old friend the speaker shared an adventure with.</summary>
    Friend = 1 << 11,

    /// <summary>A shared deed, as people say it: "that dungeon run", "that red we dropped".</summary>
    Deed = 1 << 12,

    /// <summary>A token no caller fills, such as a typo in a talk file. The line is never said.</summary>
    Unknown = 1 << 30
}

/// <summary>
/// The real state one line is filled from. A slot left null is empty, and a line that names an
/// empty slot is not picked, so no line ever says "{foe}" or a made-up stand-in.
/// </summary>
public readonly record struct TalkSlots
{
    public string Name { get; init; }

    public string Target { get; init; }

    public string Place { get; init; }

    public string Item { get; init; }

    public string Price { get; init; }

    public string Town { get; init; }

    public string Dungeon { get; init; }

    public string Foe { get; init; }

    public int? Count { get; init; }

    public string Guild { get; init; }

    public string Doing { get; init; }

    public string Friend { get; init; }

    public string Deed { get; init; }

    public const char TokenOpen = '{';
    public const char TokenClose = '}';

    /// <summary>Each slot's token is its name in lower case: TalkSlot.Foe is written {foe}.</summary>
    private static readonly IReadOnlyDictionary<string, TalkSlot> Tokens = BuildTokens();

    /// <summary>The slots this state fills.</summary>
    public TalkSlot Filled
    {
        get
        {
            var filled = TalkSlot.None;

            foreach (var slot in Tokens.Values)
            {
                if (!string.IsNullOrWhiteSpace(ValueOf(slot)))
                {
                    filled |= slot;
                }
            }

            return filled;
        }
    }

    /// <summary>The slots a template names. An unknown token marks the line <see cref="TalkSlot.Unknown"/>.</summary>
    public static TalkSlot Needs(string template)
    {
        var needs = TalkSlot.None;

        if (string.IsNullOrEmpty(template))
        {
            return needs;
        }

        var open = template.IndexOf(TokenOpen);

        while (open >= 0)
        {
            var close = template.IndexOf(TokenClose, open + 1);

            if (close < 0)
            {
                return needs;
            }

            var token = template.Substring(open, close - open + 1);
            needs |= Tokens.TryGetValue(token, out var slot) ? slot : TalkSlot.Unknown;
            open = template.IndexOf(TokenOpen, close + 1);
        }

        return needs;
    }

    /// <summary>The template with every filled slot written in.</summary>
    public string Fill(string template)
    {
        if (string.IsNullOrEmpty(template) || template.IndexOf(TokenOpen) < 0)
        {
            return template;
        }

        foreach (var (token, slot) in Tokens)
        {
            if (template.Contains(token, StringComparison.Ordinal))
            {
                template = template.Replace(token, ValueOf(slot), StringComparison.Ordinal);
            }
        }

        return template;
    }

    /// <summary>How a slot is written in a talk file: {foe}.</summary>
    public static string TokenOf(TalkSlot slot) =>
        $"{TokenOpen}{slot.ToString().ToLowerInvariant()}{TokenClose}";

    private static Dictionary<string, TalkSlot> BuildTokens()
    {
        var tokens = new Dictionary<string, TalkSlot>(StringComparer.Ordinal);

        foreach (var slot in Enum.GetValues<TalkSlot>())
        {
            if (slot is not (TalkSlot.None or TalkSlot.Unknown))
            {
                tokens[TokenOf(slot)] = slot;
            }
        }

        return tokens;
    }

    private string ValueOf(TalkSlot slot) =>
        slot switch
        {
            TalkSlot.Name => Name,
            TalkSlot.Target => Target,
            TalkSlot.Place => Place,
            TalkSlot.Item => Item,
            TalkSlot.Price => Price,
            TalkSlot.Town => Town,
            TalkSlot.Dungeon => Dungeon,
            TalkSlot.Foe => Foe,
            TalkSlot.Count => Count?.ToString(CultureInfo.InvariantCulture),
            TalkSlot.Guild => Guild,
            TalkSlot.Doing => Doing,
            TalkSlot.Friend => Friend,
            TalkSlot.Deed => Deed,
            _ => null
        };
}
