using System;
using System.Collections.Generic;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class TypingStyleTests
{
    private const int Seed = 12345;
    private const int SeedSweep = 400;
    private const int ProfileSample = 300;
    private const int AlwaysPercent = 100;
    private const int MinDistinctForms = 12;

    private static readonly TypingProfile Sloppy = new(
        TypingCase.Lower,
        TypingProfile.HeavyAbbreviations,
        TypingProfile.ThanksShort,
        TypingEnd.Drop,
        0,
        null,
        0,
        0
    );

    [Fact]
    public void Apply_LowerCaseAndShortForms()
    {
        var typed = TypingStyle.Apply("Thank you, are you going to Britain tonight?", Sloppy, Seed);

        Assert.Equal("ty, r u going to britain tonite?", typed);
    }

    [Fact]
    public void Apply_PlainProfileLeavesTheLineAlone()
    {
        const string line = "Well met, Bob. Your axe is keen!";

        Assert.Equal(line, TypingStyle.Apply(line, TypingProfile.Plain, Seed));
    }

    [Fact]
    public void Apply_DropEndsWithoutAFullStop_AndKeepsTheQuestion()
    {
        Assert.Equal("see u at the bank", TypingStyle.Apply("See you at the bank.", Sloppy, Seed));
        Assert.Equal("where r u?", TypingStyle.Apply("Where are you?", Sloppy, Seed));
    }

    [Fact]
    public void Apply_DoubleAndTrailEndings()
    {
        var shouter = TypingProfile.Plain with { End = TypingEnd.Double };
        var drifter = TypingProfile.Plain with { End = TypingEnd.Trail };

        Assert.Equal("Help me!!", TypingStyle.Apply("Help me!", shouter, Seed));
        Assert.Equal("Long day", TypingStyle.Apply("Long day.", shouter, Seed));
        Assert.Equal("Long day...", TypingStyle.Apply("Long day.", drifter, Seed));
    }

    [Fact]
    public void Apply_GuardedLineKeepsNumbersAndCapitalisedNames()
    {
        const string trade = "WTS Katana of Vanquishing for 5000gp, you want it?";
        var typed = TypingStyle.Apply(trade, Sloppy, Seed, guarded: true);

        Assert.Equal("WTS Katana of Vanquishing for 5000gp, u want it?", typed);
    }

    [Fact]
    public void Apply_NeverChangesAWordWithADigit()
    {
        var wild = new TypingProfile(TypingCase.RareCaps, TypingProfile.HeavyAbbreviations, TypingProfile.ThanksClipped,
            TypingEnd.Double, AlwaysPercent, TypingProfile.TailLol, AlwaysPercent, AlwaysPercent);

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            var typed = TypingStyle.Apply("selling 250 bandages at 12gp each", wild, seed);

            Assert.Contains("250", typed);
            Assert.Contains("12gp", typed);
        }
    }

    [Fact]
    public void Apply_TypoSwapsTwoInnerLettersOfOneWord()
    {
        var clumsy = TypingProfile.Plain with { TypoPercent = AlwaysPercent };
        const string line = "mining here";

        for (var seed = 0; seed < SeedSweep; seed++)
        {
            var typed = TypingStyle.Apply(line, clumsy, seed).Split(' ');
            var written = line.Split(' ');
            var changed = 0;

            for (var i = 0; i < written.Length; i++)
            {
                if (typed[i] == written[i])
                {
                    continue;
                }

                changed++;
                Assert.Equal(written[i][0], typed[i][0]);
                Assert.Equal(written[i][^1], typed[i][^1]);
                Assert.Equal(Sorted(written[i]), Sorted(typed[i]));
            }

            Assert.Equal(1, changed);
        }
    }

    [Fact]
    public void Apply_NoSlipTailOrStretchOnAGuardedLine()
    {
        var wild = new TypingProfile(TypingCase.Normal, TypingProfile.NoAbbreviations, TypingProfile.ThanksShort,
            TypingEnd.Keep, AlwaysPercent, TypingProfile.TailLol, AlwaysPercent, AlwaysPercent);

        Assert.Equal("guards help", TypingStyle.Apply("guards help", wild, Seed, guarded: true));
    }

    [Fact]
    public void Apply_TailAndStretch()
    {
        var laugher = TypingProfile.Plain with { Tail = TypingProfile.TailHeh, TailPercent = AlwaysPercent };
        var stretcher = TypingProfile.Plain with { EmphasisPercent = AlwaysPercent };

        Assert.Equal("nice find heh", TypingStyle.Apply("nice find", laugher, Seed));
        Assert.Equal("where to?", TypingStyle.Apply("where to?", laugher, Seed));
        Assert.Equal("nice find lol", TypingStyle.Apply("nice find lol", laugher, Seed));
        Assert.Equal("so tired", TypingStyle.Apply("so tired", TypingProfile.Plain, Seed));
        Assert.Equal("sooo tired", TypingStyle.Apply("so tired", stretcher, Seed));
    }

    [Fact]
    public void Apply_ManyPeopleTypeOneLineManyWays()
    {
        var forms = new HashSet<string>();

        for (var i = 0; i < ProfileSample; i++)
        {
            forms.Add(TypingStyle.Apply("Thanks for the help, see you around.", TypingProfile.For($"Felucca:connor#{i}"), i));
        }

        Assert.True(forms.Count >= MinDistinctForms);
    }

    private static string Sorted(string word)
    {
        var letters = word.ToCharArray();
        Array.Sort(letters);
        return new string(letters);
    }

    [Fact]
    public void For_IsStablePerIdAndVariesAcrossPeople()
    {
        Assert.Equal(TypingProfile.For("Felucca:connor#3"), TypingProfile.For("Felucca:connor#3"));
        Assert.Equal(TypingProfile.Plain, TypingProfile.For(null));

        var shapes = new HashSet<TypingProfile>();

        for (var i = 0; i < ProfileSample; i++)
        {
            shapes.Add(TypingProfile.For($"Felucca:connor#{i}"));
        }

        Assert.True(shapes.Count > ProfileSample / 2);
    }
}
