using Server;
using Server.Items;
using SosariaAI.Economy;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// How treasure maps and bottles are named in a sale line. The lines themselves live in the
/// talk library (<see cref="TalkCategory.SosHawk"/>, <see cref="TalkCategory.TreasureCannotRead"/>).
/// </summary>
public static class TreasureLines
{
    public const string SosNoun = "sos";
    public const string MapNounFormat = "lvl {0} tmap";

    /// <summary>What the goods are called aloud: "lvl 3 tmap", "sos", or the market's name.</summary>
    public static string NounOf(Item item) =>
        item switch
        {
            TreasureMap map => string.Format(MapNounFormat, map.Level),
            SOS => SosNoun,
            _ => Appraisal.NounOf(item)
        };

    /// <summary>The slots of a sale line: the goods by name and the asking price said the 1999 way.</summary>
    public static TalkSlots Sale(Item goods, int asking) =>
        new() { Item = NounOf(goods), Price = GoldWords.Spoken(asking) };
}
