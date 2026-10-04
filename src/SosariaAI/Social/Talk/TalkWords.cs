using System;
using Server;
using Server.Regions;
using SosariaAI.Memory;

namespace SosariaAI.Social;

/// <summary>How people name things aloud, for talk slots: "lich" not "a lich", "despise" not "Despise, Felucca".</summary>
public static class TalkWords
{
    private const string DungeonDeed = "that dungeon run";
    private const string HuntDeed = "that hunt";
    private const string RedKillDeed = "that red we dropped";
    private const string DeathDeed = "that time we got wrecked";
    private const string RescueDeed = "that rez";
    private const string DuelDeed = "that duel";
    private const string HouseDeed = "placing the house";
    private const string FirstKillDeed = "that first kill";
    private const string OtherDeed = "that trip";

    private static readonly string[] Articles = ["a ", "an ", "the "];

    /// <summary>A creature's name without its article, lower case; a person's name as it is.</summary>
    public static string Foe(Mobile foe)
    {
        if (foe == null || string.IsNullOrWhiteSpace(foe.Name))
        {
            return null;
        }

        return foe.Player ? foe.Name : WithoutArticle(foe.Name);
    }

    public static string WithoutArticle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var spoken = name.Trim().ToLowerInvariant();

        foreach (var article in Articles)
        {
            if (spoken.StartsWith(article, StringComparison.Ordinal) && spoken.Length > article.Length)
            {
                return spoken[article.Length..];
            }
        }

        return spoken;
    }

    /// <summary>The region, landmark or town the mobile stands at, as people say it.</summary>
    public static string Place(Mobile mobile) =>
        mobile == null ? null : GossipLines.PlaceWord(PlaceNames.Of(mobile));

    /// <summary>The town the mobile stands in, or null outside every town.</summary>
    public static string Town(Mobile mobile)
    {
        var town = mobile?.Region?.GetRegion<TownRegion>()?.Name;
        return string.IsNullOrWhiteSpace(town) ? null : town.ToLowerInvariant();
    }

    /// <summary>A shared adventure as friends name it aloud: "that dungeon run", "that red we dropped".</summary>
    public static string Deed(string kind) =>
        kind switch
        {
            AdventureKinds.Dungeon => DungeonDeed,
            AdventureKinds.Hunt => HuntDeed,
            AdventureKinds.RedKill => RedKillDeed,
            AdventureKinds.Death => DeathDeed,
            AdventureKinds.Rescue => RescueDeed,
            AdventureKinds.Duel => DuelDeed,
            AdventureKinds.House => HouseDeed,
            AdventureKinds.FirstKill => FirstKillDeed,
            _ => OtherDeed
        };

    /// <summary>
    /// A remembered place as people say it ("despise"), or null for the wild: a line that names
    /// a place is then passed over.
    /// </summary>
    public static string RememberedPlace(string place)
    {
        var spoken = GossipLines.PlaceWord(place);
        return spoken == PlaceNameRules.Wild ? null : spoken;
    }
}
