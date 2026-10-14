using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class HarvestEngineMessageTests
{
    /// <summary>"That is too far away": the engine's refusal of a swing out of its reach.</summary>
    private const int TooFarAway = 500446;

    private const string TooFarAwayText = "500446";

    [Fact]
    public void MessageText_AClilocIsWrittenAsItsNumber()
    {
        Assert.Equal(TooFarAwayText, HarvestSkill.MessageText(TextDefinition.Of(TooFarAway)));
    }

    [Fact]
    public void MessageText_APlainMessageIsWrittenAsItsText()
    {
        Assert.Equal(
            HarvestSkill.HarvestCheckRefusal.String,
            HarvestSkill.MessageText(HarvestSkill.HarvestCheckRefusal)
        );
    }

    [Fact]
    public void MessageText_NoMessageWritesNothing()
    {
        Assert.Null(HarvestSkill.MessageText(null));
        Assert.Null(HarvestSkill.MessageText(TextDefinition.Empty));
    }

    [Fact]
    public void Refusals_EveryNamedRefusalHasItsOwnWords()
    {
        TextDefinition[] refusals =
        [
            HarvestSkill.HarvestCheckRefusal,
            HarvestSkill.BadTargetRefusal,
            HarvestSkill.OutOfReachRefusal,
            HarvestSkill.NoStockRefusal,
            HarvestSkill.SwingRunningRefusal
        ];

        for (var i = 0; i < refusals.Length; i++)
        {
            Assert.False(refusals[i].IsEmpty);

            for (var k = i + 1; k < refusals.Length; k++)
            {
                Assert.NotEqual(refusals[i].String, refusals[k].String);
            }
        }
    }
}
