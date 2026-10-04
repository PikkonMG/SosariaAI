using System;
using System.Collections.Generic;
using System.Linq;
using Server.Guilds;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class GuildCatalogTests
{
    private const int GuildCount = 26;
    private const int FactionPercentLow = 15;
    private const int FactionPercentHigh = 25;
    private const int SmallGuildWeight = 1;
    private const int SeedSample = 2000;
    private const int SidePercentLow = 11;
    private const int SidePercentHigh = 16;
    private const int MembershipPercentLow = 34;
    private const int MembershipPercentHigh = 46;
    private const int PercentScale = 100;

    [Fact]
    public void Catalog_KeepsTheFirstFourAndAddsEraGuilds()
    {
        Assert.Equal(0.40, GuildCatalog.MembershipChance);
        Assert.Equal(-1, GuildCatalog.None);
        Assert.Equal(GuildCount, GuildCatalog.All.Length);
        Assert.Equal("Britain Militia", GuildCatalog.All[0].Name);
        Assert.Equal("BM", GuildCatalog.All[0].Tag);
        Assert.Equal("West Bank Crew", GuildCatalog.All[1].Name);
        Assert.Equal("Graveyard Watch", GuildCatalog.All[2].Name);
        Assert.Equal("Despise Delvers", GuildCatalog.All[3].Name);

        for (var i = 0; i < GuildCatalog.All.Length; i++)
        {
            Assert.Equal(i, GuildCatalog.All[i].Index);
        }
    }

    [Fact]
    public void Catalog_TagsAreUniqueAndFindable()
    {
        var tags = new HashSet<string>();

        foreach (var record in GuildCatalog.All)
        {
            Assert.True(tags.Add(record.Tag));
            Assert.Equal(record.Index, GuildCatalog.IndexOfTag(record.Tag.ToLowerInvariant()));
        }

        Assert.Equal(GuildCatalog.None, GuildCatalog.IndexOfTag("NOPE"));
        Assert.Equal(GuildCatalog.None, GuildCatalog.IndexOfTag(null));
    }

    [Fact]
    public void Catalog_HasBothSidesAndOneThievesCrew()
    {
        var order = 0;
        var chaos = 0;
        var thieves = 0;

        foreach (var record in GuildCatalog.All)
        {
            order += record.Alignment == GuildType.Order ? 1 : 0;
            chaos += record.Alignment == GuildType.Chaos ? 1 : 0;
            thieves += record.Thieves ? 1 : 0;
        }

        Assert.True(order >= 2);
        Assert.True(chaos >= 2);
        Assert.Equal(1, thieves);
        Assert.True(GuildCatalog.All[GuildCatalog.ThievesIndex].Thieves);
        Assert.Equal(0, GuildCatalog.All[GuildCatalog.ThievesIndex].Weight);
    }

    [Fact]
    public void Opposed_OnlyOrderAgainstChaos()
    {
        Assert.True(GuildCatalog.Opposed(GuildType.Order, GuildType.Chaos));
        Assert.True(GuildCatalog.Opposed(GuildType.Chaos, GuildType.Order));
        Assert.False(GuildCatalog.Opposed(GuildType.Order, GuildType.Order));
        Assert.False(GuildCatalog.Opposed(GuildType.Regular, GuildType.Chaos));
        Assert.False(GuildCatalog.Opposed(GuildType.Regular, GuildType.Regular));
        Assert.Equal(GuildType.Regular, GuildCatalog.AlignmentOf(GuildCatalog.None));
    }

    [Fact]
    public void Pick_RollAtOrAboveForty_IsNone()
    {
        Assert.Equal(GuildCatalog.None, GuildCatalog.Pick(40, 0));
        Assert.Equal(GuildCatalog.None, GuildCatalog.Pick(100, 0));
    }

    [Fact]
    public void Pick_RollBelowForty_JoinsAnOrdinaryGuild()
    {
        for (var roll = 0; roll < GuildCatalog.WeightRollSides; roll++)
        {
            var pick = GuildCatalog.Pick(0, roll);
            Assert.NotEqual(GuildCatalog.None, pick);
            Assert.False(GuildCatalog.All[pick].Thieves);
        }
    }

    [Fact]
    public void Pick_WeightsFavourTheBigGuilds()
    {
        var counts = new int[GuildCatalog.All.Length];

        for (var roll = 0; roll < GuildCatalog.WeightRollSides; roll++)
        {
            counts[GuildCatalog.Pick(0, roll)]++;
        }

        Assert.True(counts[0] > counts[3]);
        Assert.True(counts[4] > counts[6]);
        Assert.True(counts[1] > counts[11]);
    }

    [Fact]
    public void Catalog_IsMostlyOrdinaryGuilds_AndManySmallOnes()
    {
        var faction = GuildCatalog.All.Count(record => record.Alignment != GuildType.Regular);
        var small = GuildCatalog.All.Count(record => record.Weight == SmallGuildWeight);

        Assert.InRange(faction * PercentScale / GuildCatalog.All.Length, FactionPercentLow, FactionPercentHigh);
        Assert.True(small * 2 >= GuildCatalog.All.Length, $"{small} small guilds of {GuildCatalog.All.Length}");
    }

    [Fact]
    public void TakesMember_OrderTakesNoMurderer_ChaosTakesBoth()
    {
        Assert.False(GuildCatalog.TakesMember(GuildType.Order, murderer: true));
        Assert.True(GuildCatalog.TakesMember(GuildType.Order, murderer: false));
        Assert.True(GuildCatalog.TakesMember(GuildType.Chaos, murderer: true));
        Assert.True(GuildCatalog.TakesMember(GuildType.Regular, murderer: true));
    }

    [Fact]
    public void SeededMurderers_NeverOrder_SomeChaos_AndStable()
    {
        var chaos = 0;

        for (var i = 0; i < SeedSample; i++)
        {
            var id = $"red-copy#{i}";
            var guild = GuildCatalog.SeedFor(id, thief: false, fighter: true, murderer: true);

            Assert.NotEqual(GuildType.Order, GuildCatalog.AlignmentOf(guild));
            Assert.Equal(guild, GuildCatalog.Settle(id, thief: false, fighter: true, guild, murderer: true));
            chaos += GuildCatalog.AlignmentOf(guild) == GuildType.Chaos ? 1 : 0;
        }

        Assert.True(chaos > 0);
    }

    [Fact]
    public void SeededFighters_AboutOneInSevenOnEachSide()
    {
        var order = 0;
        var chaos = 0;

        for (var i = 0; i < SeedSample; i++)
        {
            var side = GuildCatalog.AlignmentOf(GuildCatalog.SeedFor($"britain-copy#{i}", thief: false, fighter: true));
            order += side == GuildType.Order ? 1 : 0;
            chaos += side == GuildType.Chaos ? 1 : 0;
        }

        Assert.InRange(order * PercentScale / SeedSample, SidePercentLow, SidePercentHigh);
        Assert.InRange(chaos * PercentScale / SeedSample, SidePercentLow, SidePercentHigh);
    }

    [Fact]
    public void WeightTotal_DividesTheRoll_SoTheModuloStaysEven()
    {
        Assert.Equal(0, GuildCatalog.WeightRollSides % GuildCatalog.All.Sum(record => record.Weight));
    }

    [Fact]
    public void Settle_MovesAnUntouchedFirstRoll_AndKeepsWhatAFriendChose()
    {
        var moved = 0;

        for (var i = 0; i < SeedSample; i++)
        {
            var id = $"britain-copy#{i}";
            var seed = GuildCatalog.SeedFor(id, thief: false, fighter: true);

            // A new person, or one the catalog no longer knows, takes the roll.
            Assert.Equal(seed, GuildCatalog.Settle(id, thief: false, fighter: true, GuildCatalog.None));
            Assert.Equal(seed, GuildCatalog.Settle(id, thief: false, fighter: true, GuildCount + 1));

            for (var saved = 0; saved < GuildCatalog.All.Length; saved++)
            {
                var settled = GuildCatalog.Settle(id, thief: false, fighter: true, saved);

                // Every boot agrees: a second pass changes nothing.
                Assert.Equal(settled, GuildCatalog.Settle(id, thief: false, fighter: true, settled));

                // Order and Chaos come from the roll alone.
                Assert.True(GuildCatalog.AlignmentOf(settled) == GuildType.Regular || settled == seed);
                moved += settled == saved ? 0 : 1;
            }
        }

        Assert.True(moved > 0);
    }

    [Fact]
    public void Settle_KeepsARegularGuildAFriendBroughtAPersonInto()
    {
        var id = FindId(candidate => GuildCatalog.SeedFor(candidate, thief: false, fighter: true) == GuildCatalog.None);
        var friends = FindRegularGuildOtherThanFirstRoll(id);

        Assert.Equal(friends, GuildCatalog.Settle(id, thief: false, fighter: true, friends));
        Assert.True(GuildCatalog.TakesFriends(friends));
    }

    [Fact]
    public void TakesFriends_OnlyRegularGuilds()
    {
        foreach (var record in GuildCatalog.All)
        {
            Assert.Equal(record.Alignment == GuildType.Regular, GuildCatalog.TakesFriends(record.Index));
        }

        Assert.False(GuildCatalog.TakesFriends(GuildCatalog.None));
    }

    [Fact]
    public void SeedFor_ANonFighterIsNeverOrderOrChaos_AndKeepsItsMembership()
    {
        var movedOffASide = 0;

        for (var i = 0; i < SeedSample; i++)
        {
            var id = $"britain-copy#{i}";
            var fighterRoll = GuildCatalog.SeedFor(id, thief: false, fighter: true);
            var crafterRoll = GuildCatalog.SeedFor(id, thief: false, fighter: false);

            Assert.Equal(GuildType.Regular, GuildCatalog.AlignmentOf(crafterRoll));
            Assert.Equal(fighterRoll == GuildCatalog.None, crafterRoll == GuildCatalog.None);

            if (GuildCatalog.AlignmentOf(fighterRoll) == GuildType.Regular)
            {
                Assert.Equal(fighterRoll, crafterRoll);
            }
            else
            {
                movedOffASide++;
            }
        }

        Assert.True(movedOffASide > 0);
    }

    [Fact]
    public void Settle_TakesANonFighterOutOfOrderAndChaos()
    {
        for (var i = 0; i < SeedSample; i++)
        {
            var id = $"britain-copy#{i}";

            for (var saved = GuildCatalog.None; saved < GuildCatalog.All.Length; saved++)
            {
                var settled = GuildCatalog.Settle(id, thief: false, fighter: false, saved);

                Assert.Equal(GuildType.Regular, GuildCatalog.AlignmentOf(settled));
                Assert.Equal(settled, GuildCatalog.Settle(id, thief: false, fighter: false, settled));
            }
        }
    }

    [Fact]
    public void SeedFor_IsStableAndNearFortyPercent_LeavingMostUnguilded()
    {
        var members = 0;

        for (var i = 0; i < SeedSample; i++)
        {
            var id = $"britain-copy#{i}";
            var guild = GuildCatalog.SeedFor(id, thief: false, fighter: true);
            Assert.Equal(guild, GuildCatalog.SeedFor(id, thief: false, fighter: true));
            members += guild == GuildCatalog.None ? 0 : 1;
        }

        var percent = members * PercentScale / SeedSample;
        Assert.InRange(percent, MembershipPercentLow, MembershipPercentHigh);
    }

    [Fact]
    public void SeedFor_MostThievesWearTheCrewTag()
    {
        var tagged = 0;

        for (var i = 0; i < SeedSample; i++)
        {
            var guild = GuildCatalog.SeedFor($"thief-copy#{i}", thief: true, fighter: false);
            tagged += guild == GuildCatalog.ThievesIndex ? 1 : 0;
            Assert.NotEqual(GuildCatalog.ThievesIndex, GuildCatalog.SeedFor($"thief-copy#{i}", thief: false, fighter: true));
        }

        Assert.True(tagged * PercentScale / SeedSample >= GuildCatalog.ThiefTagPercent - 6);
    }

    [Fact]
    public void ThievesGuild_EnrollsMostThievesAndNoOneElse()
    {
        var enrolled = 0;

        for (var i = 0; i < SeedSample; i++)
        {
            enrolled += ThievesGuild.Enrolls($"thief-copy#{i}", thief: true) ? 1 : 0;
            Assert.False(ThievesGuild.Enrolls($"thief-copy#{i}", thief: false));
        }

        Assert.True(enrolled * PercentScale / SeedSample >= ThievesGuild.EnrollPercent - 6);
    }

    private static string FindId(Func<string, bool> wanted)
    {
        for (var i = 0; i < SeedSample; i++)
        {
            var id = $"friend-copy#{i}";

            if (wanted(id))
            {
                return id;
            }
        }

        throw new Xunit.Sdk.XunitException("no id matched");
    }

    /// <summary>A regular guild that is not this person's first-catalog roll, so only a friend could have brought it in.</summary>
    private static int FindRegularGuildOtherThanFirstRoll(string id)
    {
        for (var index = 0; index < GuildCatalog.All.Length; index++)
        {
            if (GuildCatalog.TakesFriends(index) && GuildCatalog.Settle(id, thief: false, fighter: true, index) == index)
            {
                return index;
            }
        }

        throw new Xunit.Sdk.XunitException("no regular guild kept");
    }
}
