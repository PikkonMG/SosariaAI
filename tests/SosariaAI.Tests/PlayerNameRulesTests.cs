using System;
using System.Collections.Generic;
using System.Linq;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class PlayerNameRulesTests
{
    private const int Shard = 1500;

    private static List<string> NameAShard(int count)
    {
        var worn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();

        for (var i = 0; i < count; i++)
        {
            var name = PlayerNameRules.Pick($"Felucca:p#{i}", female: i % 3 == 0, worn.Contains);
            worn.Add(name);
            names.Add(name);
        }

        return names;
    }

    [Fact]
    public void Pick_GivesAWholeShardUniqueNames()
    {
        var names = NameAShard(Shard);

        Assert.Equal(Shard, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Pick_EveryNamePassesTheEnginesPlayerNameCheck()
    {
        Assert.All(NameAShard(Shard), name => Assert.True(PlayerNameRules.IsValid(name), name));
    }

    [Fact]
    public void Pick_MixesPlayerStyles()
    {
        var names = NameAShard(Shard);

        Assert.Contains(names, name => name.Contains(PlayerNameRules.TownJoin));
        Assert.Contains(names, name => name == name.ToLowerInvariant());
        Assert.Contains(names, name => !name.Contains(' '));
        Assert.Contains(names, name => name.Split(' ').Length == 2 && PlayerNames.Surnames.Contains(name.Split(' ')[1]));
        Assert.Contains(names, name => name.StartsWith(PlayerNameRules.KnightTitle) || name.StartsWith(PlayerNameRules.DameTitle));
    }

    [Fact]
    public void Pick_IsStableForOneIdAndSkipsATakenName()
    {
        var wanted = PlayerNameRules.Pick("Felucca:connor#2", female: true, _ => false);
        var other = PlayerNameRules.Pick("Felucca:connor#2", female: true, name => name == wanted);

        Assert.Equal(wanted, PlayerNameRules.Pick("Felucca:connor#2", female: true, null));
        Assert.NotEqual(wanted, other);
        Assert.True(PlayerNameRules.IsValid(other));
    }

    [Fact]
    public void Pick_FindsAFreeNameWhenEveryRolledCandidateIsTaken()
    {
        var name = PlayerNameRules.Pick("Felucca:p#7", female: false, candidate => !candidate.Contains(' ') || candidate.Contains(" of "));

        Assert.Contains(' ', name);
        Assert.True(PlayerNameRules.IsValid(name));
    }

    [Fact]
    public void NameLists_HoldNoDuplicates_AndPlainNamesAreValid()
    {
        foreach (var list in new[] { PlayerNames.Male, PlayerNames.Female, PlayerNames.Surnames, PlayerNames.Casual, PlayerNames.Handles })
        {
            Assert.Equal(list.Length, list.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        Assert.All(PlayerNames.Male.Concat(PlayerNames.Female), name => Assert.True(PlayerNameRules.IsValid(name), name));
        Assert.All(PlayerNames.Casual, name => Assert.True(PlayerNameRules.IsValid(name), name));
        Assert.All(PlayerNames.Handles, name => Assert.True(PlayerNameRules.IsValid(name), name));
    }

    [Fact]
    public void Names_NeverReadAsAWordOfTheLine()
    {
        // A tamer named "me" wrote "me says: heh, tamed a whole pen of chickens".
        string[] pronouns = ["me", "i", "you", "he", "she", "it", "we", "they", "him", "her", "us", "them", "nobody", "someone", "everyone"];

        foreach (var pronoun in pronouns)
        {
            Assert.DoesNotContain(pronoun, PlayerNames.Casual, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(pronoun, PlayerNames.Handles, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(pronoun, NameAShard(Shard), StringComparer.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("Robard Fairbairn", "Robard")]
    [InlineData("Terrin of Jhelom", "Terrin")]
    [InlineData("Gudrun the Green", "Gudrun")]
    [InlineData("Dame Hedda", "Hedda")]
    [InlineData("Sir Thaddeus", "Thaddeus")]
    [InlineData("xXStormbringerXx", "Stormbringer")]
    public void CallingName_IsTheNamePeopleSay(string name, string called) =>
        Assert.Equal(called, PlayerNameRules.CallingName(name));

    [Theory]
    [InlineData("deathstalker")]
    [InlineData("Big Tom")]
    [InlineData("mr grumbles")]
    [InlineData("")]
    [InlineData(null)]
    public void CallingName_IsNoneForAOneWordNameOrAHandle(string name) =>
        Assert.Null(PlayerNameRules.CallingName(name));
}
