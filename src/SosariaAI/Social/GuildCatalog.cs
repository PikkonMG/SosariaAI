using System;
using Server.Guilds;
using SosariaAI.Spawning;

namespace SosariaAI.Social;

public sealed class GuildRecord
{
    public int Index { get; init; }
    public string Name { get; init; }
    public string Tag { get; init; }

    /// <summary>Share of the ordinary membership roll. Zero keeps a guild out of that roll.</summary>
    public int Weight { get; init; }

    /// <summary>Order and Chaos fight on sight. Regular guilds only fight declared wars.</summary>
    public GuildType Alignment { get; init; } = GuildType.Regular;

    /// <summary>A thieves' crew: its members come from the thief roll, not the ordinary one.</summary>
    public bool Thieves { get; init; }
}

/// <summary>
/// The shard's player guilds as the period had them: many ordinary guilds (a bank crew, a
/// crafting circle, a hunting party, a PvP guild that fought declared wars, a handful of
/// friends) and the two big Order and Chaos zergs. The sides weigh enough that about one
/// fighter in seven wears each (<see cref="SideWeight"/>). Many small guilds and one thieves'
/// crew; most people wore no tag at all. The names are this plugin's own.
/// Membership is seeded from the character id, so the same person wears the same tag after
/// every boot.
/// </summary>
public static class GuildCatalog
{
    public const double MembershipChance = 0.40;
    public const int None = -1;

    /// <summary>The sides of the weight roll. Wider than any weight total, so the modulo stays even.</summary>
    public const int WeightRollSides = 1000;

    /// <summary>Most thieves wear their crew's tag; the rest roll like anyone else.</summary>
    public const int ThiefTagPercent = 60;

    private const int PercentScale = 100;
    private const int MembershipSalt = 0x6B1;
    private const int WeightSalt = 0x6B2;
    private const int ThiefTagSalt = 0x6B3;

    /// <summary>
    /// The weight of each big side guild. With one fighter in twenty-five per side, the owner sat
    /// at the Britain bank and in the wild for hours and saw no Order and Chaos fight: about
    /// forty a side were online across the whole shard, and a war band found no free mate.
    /// </summary>
    public const int SideWeight = 33;

    public static readonly GuildRecord[] All =
    [
        new() { Index = 0, Name = "Britain Militia", Tag = "BM", Weight = 3 },
        new() { Index = 1, Name = "West Bank Crew", Tag = "WBC", Weight = 2 },
        new() { Index = 2, Name = "Graveyard Watch", Tag = "GYW", Weight = 2 },
        new() { Index = 3, Name = "Despise Delvers", Tag = "DD", Weight = 1 },
        new() { Index = 4, Name = "Legion of the Silver Oak", Tag = "LSO", Weight = SideWeight, Alignment = GuildType.Order },
        new() { Index = 5, Name = "Black Tide Brotherhood", Tag = "BTB", Weight = SideWeight, Alignment = GuildType.Chaos },
        new() { Index = 6, Name = "Wardens of the Serpent Gate", Tag = "WSG", Weight = 1, Alignment = GuildType.Order },
        new() { Index = 7, Name = "Crimson Hollow", Tag = "CH", Weight = 1, Alignment = GuildType.Chaos },
        new() { Index = 8, Name = "Moonlit Arcane Circle", Tag = "MAC", Weight = 2 },
        new() { Index = 9, Name = "Iron Hill Diggers", Tag = "IHD", Weight = 2 },
        new() { Index = 10, Name = "Vesper Coin Guild", Tag = "VCG", Weight = 2 },
        new() { Index = 11, Name = "Dawn Road Riders", Tag = "DRR", Weight = 1 },
        new() { Index = 12, Name = "Ash Wolves", Tag = "AW", Weight = 1 },
        new() { Index = 13, Name = "Quiet Hand", Tag = "QH", Weight = 0, Thieves = true },
        new() { Index = 14, Name = "Moonglow Scribes", Tag = "MGS", Weight = 2 },
        new() { Index = 15, Name = "Skara Brae Anglers", Tag = "SBA", Weight = 1 },
        new() { Index = 16, Name = "Yew Woodwardens", Tag = "YWW", Weight = 1 },
        new() { Index = 17, Name = "Cove Road Hunters", Tag = "CRH", Weight = 2 },
        new() { Index = 18, Name = "Serpent's Hold Blades", Tag = "SHB", Weight = 1 },
        new() { Index = 19, Name = "Hart and Hound", Tag = "HnH", Weight = 1 },
        new() { Index = 20, Name = "Jhelom Pit Fighters", Tag = "JPF", Weight = 2 },
        new() { Index = 21, Name = "Nujel'm Silk Traders", Tag = "NST", Weight = 1 },
        new() { Index = 22, Name = "Minoc Forge Hands", Tag = "MFH", Weight = 2 },
        new() { Index = 23, Name = "Lantern Keepers", Tag = "LK", Weight = 1 },
        new() { Index = 24, Name = "Trinsic Shield Wall", Tag = "TSW", Weight = 1 },
        new() { Index = 25, Name = "Wind Chime Circle", Tag = "WCC", Weight = 1 }
    ];

    /// <summary>The thieves' crew, or <see cref="None"/> when the catalog has none.</summary>
    public static readonly int ThievesIndex = Array.FindIndex(All, record => record.Thieves);

    private static readonly int[] Weights = Array.ConvertAll(All, record => record.Weight);

    /// <summary>The weights for a person who does not fight: Order and Chaos weigh nothing.</summary>
    private static readonly int[] RegularWeights =
        Array.ConvertAll(All, record => record.Alignment == GuildType.Regular ? record.Weight : 0);

    /// <summary>The weights for a murderer: Order weighs nothing, since Order takes no red (the operator's rule).</summary>
    private static readonly int[] NoOrderWeights =
        Array.ConvertAll(All, record => record.Alignment == GuildType.Order ? 0 : record.Weight);

    /// <summary>
    /// The weights of the first catalog, when two guilds in three were Order or Chaos. Kept
    /// only to know a membership that roll gave and nothing has changed since.
    /// </summary>
    private static readonly int[] FirstMixWeights = [3, 2, 2, 1, 5, 5, 3, 3, 2, 2, 2, 1, 1, 0];

    public static int Pick(int roll100, int weightRoll) => Pick(roll100, weightRoll, Weights);

    /// <summary>
    /// The guild a person belongs to from the first boot on, rolled from its id. Order and
    /// Chaos are for fighters: a crafter, a gatherer or a thief the roll put on a side wears a
    /// regular guild instead, rolled the same way, so it keeps its membership. Crafters in the
    /// sides ran from every draw and left the war to half its members. A murderer the roll put in
    /// Order rolls again without Order (<see cref="TakesMember"/>): Chaos has reds and blues,
    /// Order only blues.
    /// </summary>
    public static int SeedFor(string characterId, bool thief, bool fighter, bool murderer = false)
    {
        var seed = Seed(characterId, thief, Weights);

        if (!TakesMember(AlignmentOf(seed), murderer))
        {
            seed = Seed(characterId, thief, NoOrderWeights);
        }

        return fighter || AlignmentOf(seed) == GuildType.Regular ? seed : Seed(characterId, thief, RegularWeights);
    }

    /// <summary>True when a guild of this alignment takes this person: Order takes no murderer.</summary>
    public static bool TakesMember(GuildType alignment, bool murderer) => !(murderer && alignment == GuildType.Order);

    /// <summary>
    /// The side a blue person wears from its first bind, known before it spawns: Order and
    /// Chaos follow the roll alone (<see cref="Settle"/>), so the home can keep the sides apart
    /// (<see cref="SideHomes"/>).
    /// </summary>
    public static GuildType SideAtSpawn(string characterId, bool thief, bool fighter) =>
        AlignmentOf(SeedFor(characterId, thief, fighter));

    /// <summary>
    /// The guild a person wears after this boot's bind. The first catalog made two guilded
    /// people in three Order or Chaos; a membership that roll gave, untouched since, moves
    /// once to the new roll. So does a saved membership the catalog no longer knows. Order
    /// and Chaos follow the roll alone, so their share stays what the catalog gives; a
    /// regular guild a person joined through a friend is kept. Every boot agrees, and a
    /// second pass changes nothing. A player's recruit is never passed here. A person who does
    /// not fight leaves Order and Chaos for its regular roll, and a murderer leaves Order
    /// (<see cref="SeedFor"/>).
    /// </summary>
    public static int Settle(string characterId, bool thief, bool fighter, int saved, bool murderer = false)
    {
        var seed = SeedFor(characterId, thief, fighter, murderer);

        if (saved < 0 || saved >= All.Length || saved == Seed(characterId, thief, FirstMixWeights) ||
            AlignmentOf(seed) != GuildType.Regular)
        {
            return seed;
        }

        return AlignmentOf(saved) == GuildType.Regular ? saved : seed;
    }

    /// <summary>A guild a friend can bring a person into: a regular one. Order and Chaos come from the roll alone.</summary>
    public static bool TakesFriends(int index) => index >= 0 && index < All.Length && All[index].Alignment == GuildType.Regular;

    public static GuildType AlignmentOf(int index) =>
        index >= 0 && index < All.Length ? All[index].Alignment : GuildType.Regular;

    /// <summary>The index of the guild wearing this tag, or <see cref="None"/>.</summary>
    public static int IndexOfTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return None;
        }

        return Array.FindIndex(All, record => string.Equals(record.Tag, tag, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Order against Chaos. Two regular guilds, or two of one side, are not opposed.</summary>
    public static bool Opposed(GuildType first, GuildType second) =>
        first != GuildType.Regular && second != GuildType.Regular && first != second;

    private static int Seed(string characterId, bool thief, int[] weights)
    {
        var thieves = ThievesIndex;

        if (thief && thieves != None && PersonDice.Chance(characterId, ThiefTagSalt, ThiefTagPercent))
        {
            return thieves;
        }

        return Pick(
            PersonDice.Percent(characterId, MembershipSalt),
            PersonDice.Roll(characterId, WeightSalt, WeightRollSides),
            weights
        );
    }

    private static int Pick(int roll100, int weightRoll, int[] weights)
    {
        if (roll100 >= (int)(MembershipChance * PercentScale))
        {
            return None;
        }

        var total = 0;

        for (var i = 0; i < weights.Length; i++)
        {
            total += Math.Max(0, weights[i]);
        }

        if (total <= 0)
        {
            return None;
        }

        var pick = ((weightRoll % total) + total) % total;
        var acc = 0;

        for (var i = 0; i < weights.Length; i++)
        {
            acc += Math.Max(0, weights[i]);

            if (pick < acc)
            {
                return All[i].Index;
            }
        }

        return All[0].Index;
    }
}
