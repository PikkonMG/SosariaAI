using System.Linq;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class PersonDiceTests
{
    private const int People = 2000;
    private const int Sides = 10;

    [Fact]
    public void Roll_StaysInRange_AndIsStable()
    {
        for (var i = 0; i < People; i++)
        {
            var roll = PersonDice.Roll($"Felucca:p#{i}", 3, Sides);
            Assert.InRange(roll, 0, Sides - 1);
            Assert.Equal(roll, PersonDice.Roll($"Felucca:p#{i}", 3, Sides));
        }

        Assert.Equal(0, PersonDice.Roll("Felucca:p#1", 3, 1));
    }

    [Fact]
    public void Rolls_WithDifferentSalts_DoNotFollowEachOther()
    {
        // A straight-line salt made the second roll a fixed step from the first.
        var matches = Enumerable.Range(0, People)
            .Count(i => (PersonDice.Roll($"Felucca:p#{i}", 1, Sides) + 1) % Sides == PersonDice.Roll($"Felucca:p#{i}", 2, Sides));

        Assert.InRange(matches, People / Sides / 2, People / Sides * 2);
    }

    [Fact]
    public void Weighted_SkipsZeroWeights()
    {
        for (var i = 0; i < People; i++)
        {
            Assert.NotEqual(1, PersonDice.Weighted($"Felucca:p#{i}", 5, 3, 0, 2));
        }
    }
}
