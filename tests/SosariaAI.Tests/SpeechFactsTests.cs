using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class SpeechFactsTests
{
    private const int FullHits = 100;

    // A living, whole character standing about: no group behind it and no trip under way.
    private static SpeechFacts.Snapshot StandingAbout(bool leadsGroup = false, bool travelling = false) =>
        new()
        {
            IsAlive = true,
            Hits = FullHits,
            HitsMax = FullHits,
            LeadsGroup = leadsGroup,
            Travelling = travelling
        };

    [Theory]
    [InlineData("you coming, tamsin?")]
    [InlineData("lfg despise, anyone up for it?")]
    [InlineData("follow me to the good spot")]
    [InlineData("stay close")]
    public void Contradicts_AnInviteNeedsAGroupBehindIt(string line)
    {
        Assert.True(SpeechFacts.Contradicts(line, StandingAbout()));
        Assert.True(SpeechFacts.Contradicts(line, StandingAbout(travelling: true)));
        Assert.False(SpeechFacts.Contradicts(line, StandingAbout(leadsGroup: true)));
    }

    [Theory]
    [InlineData("bag full, heading to bank")]
    [InlineData("off to the mine again")]
    [InlineData("just heading home")]
    public void Contradicts_ATripClaimNeedsATripUnderWay(string line)
    {
        Assert.True(SpeechFacts.Contradicts(line, StandingAbout()));
        Assert.True(SpeechFacts.Contradicts(line, StandingAbout(leadsGroup: true)));
        Assert.False(SpeechFacts.Contradicts(line, StandingAbout(travelling: true)));
    }

    [Theory]
    [InlineData("heading to minoc, u coming or what")]
    [InlineData("keep up, i dont wait long")]
    [InlineData("hail tamsin, you heading to brit too")]
    public void Contradicts_DropsTheLinesNoCodeBacked(string line)
    {
        // Leander, a red patrolling his gang's camp at Destard, said the first two to a player
        // and never left; Ansgar, standing at the Britain bank with the player, said the third.
        Assert.True(SpeechFacts.Contradicts(line, StandingAbout()));
    }

    [Fact]
    public void Contradicts_ALeaderOnItsWayMayCallItsGroup()
    {
        var facts = StandingAbout(leadsGroup: true, travelling: true);

        Assert.False(SpeechFacts.Contradicts("heading to minoc, u coming or what", facts));
        Assert.False(SpeechFacts.Contradicts("keep up, i dont wait long", facts));
    }

    [Fact]
    public void Contradicts_OrdinaryChatNeedsNoGroupOrTrip()
    {
        var facts = StandingAbout();

        Assert.False(SpeechFacts.Contradicts("hail tamsin, good to see you", facts));
        Assert.False(SpeechFacts.Contradicts("mining at minoc lol", facts));
        Assert.False(SpeechFacts.Contradicts("Walked slow so the old wolf could keep up.", facts));
    }

    [Theory]
    [InlineData("keep up, i dont wait long")]
    [InlineData("Wolves attack in packs. Stay close.")]
    [InlineData("i wont wait for stragglers")]
    public void ClaimsLeading_TrueForAGroupsPace(string line)
    {
        Assert.True(SpeechFacts.ClaimsLeading(line));
    }

    [Theory]
    [InlineData("Walked slow so the old wolf could keep up.")]
    [InlineData("dont wait up for me tonight")]
    [InlineData(null)]
    public void ClaimsLeading_FalseForOrdinaryChat(string line)
    {
        Assert.False(SpeechFacts.ClaimsLeading(line));
    }

    [Theory]
    [InlineData(SkillKinds.GoTo)]
    [InlineData(SkillKinds.Travel)]
    [InlineData(SkillKinds.GoHome)]
    [InlineData(SkillKinds.Recall)]
    [InlineData(SkillKinds.Gate)]
    [InlineData(SkillKinds.Follow)]
    [InlineData(SkillKinds.Boat)]
    [InlineData(CorpseRunSkill.SkillName)]
    [InlineData(HouseRetreatSkill.SkillName)]
    public void IsTripKind_TrueForAStepThatGetsSomewhere(string kind)
    {
        Assert.True(SpeechFacts.IsTripKind(kind));
    }

    [Theory]
    [InlineData(SkillKinds.Patrol)]
    [InlineData(SkillKinds.Conflict)]
    [InlineData(SkillKinds.BankCrowd)]
    [InlineData(SkillKinds.Loiter)]
    [InlineData(SkillKinds.Mine)]
    [InlineData(null)]
    public void IsTripKind_FalseForWorkInOnePlace(string kind)
    {
        Assert.False(SpeechFacts.IsTripKind(kind));
    }

    [Theory]
    [InlineData("hail {name}, heading to shame, come along")]
    [InlineData("hail {name}, you heading to brit too")]
    [InlineData("keep up, i dont wait long")]
    public void ClaimsFree_RejectsAWrittenPromise(string line)
    {
        Assert.False(SpeechFacts.ClaimsFree(line));
        Assert.False(SpeechFacts.AmbientSafe(line));
    }
    [Fact]
    public void Contradicts_FullLoadWithEmptyPack()
    {
        var facts = new SpeechFacts.Snapshot { PackCount = 0, HitsMax = 100, Hits = 100 };
        Assert.True(SpeechFacts.Contradicts("Fine timber today. I'm nearly a full load.", facts));
        Assert.False(SpeechFacts.Contradicts("Morning.", facts));
    }

    [Fact]
    public void Contradicts_AllowsAFullLoadWhenThePackHasGoods()
    {
        var facts = new SpeechFacts.Snapshot { PackCount = 40, HitsMax = 100, Hits = 100 };
        Assert.False(SpeechFacts.Contradicts("I'm near a full load.", facts));
    }

    [Fact]
    public void Contradicts_TheLivingCannotClaimDeathOrARez()
    {
        var facts = new SpeechFacts.Snapshot { IsAlive = true, HitsMax = 100, Hits = 100, PackCount = 10, Gold = 5 };

        Assert.True(SpeechFacts.Contradicts("rez plz im at the swamp", facts));
        Assert.True(SpeechFacts.Contradicts("i died again, lost my stuff", facts));
        Assert.True(SpeechFacts.Contradicts("lost my armor, need a rez", facts));
        Assert.True(SpeechFacts.Contradicts("im a ghost, someone rez me", facts));
        Assert.False(SpeechFacts.Contradicts("thx for the rez earlier", facts));
        Assert.False(SpeechFacts.Contradicts("rezzed three people today", facts));
    }

    [Fact]
    public void Contradicts_AGhostMayPlead()
    {
        var facts = new SpeechFacts.Snapshot { IsAlive = false };
        Assert.False(SpeechFacts.Contradicts("rez plz im at the swamp", facts));
    }

    [Fact]
    public void AmbientSafe_OnlyUnprovokedLinesPass()
    {
        Assert.False(SpeechFacts.AmbientSafe("anyone need a rez?"));
        Assert.False(SpeechFacts.AmbientSafe("dunstan's about?"));
        Assert.False(SpeechFacts.AmbientSafe("wts broadsword of power 1k"));
        Assert.False(SpeechFacts.AmbientSafe("afk a sec kids are yelling"));
        Assert.False(SpeechFacts.AmbientSafe("rez plz im at the swamp"));
        Assert.False(SpeechFacts.AmbientSafe("brb door"));
        Assert.False(SpeechFacts.AmbientSafe(null));
        Assert.True(SpeechFacts.AmbientSafe("my mace broke again heh"));
        Assert.True(SpeechFacts.AmbientSafe("bank sitting till my regs restock"));
    }

    [Fact]
    public void Contradicts_TakingOrdersOnlyWhenTheCrafterCan()
    {
        Assert.True(SpeechFacts.Contradicts("taking orders", new SpeechFacts.Snapshot { IsAlive = true, TakesOrders = false }));
        Assert.False(SpeechFacts.Contradicts("taking orders", new SpeechFacts.Snapshot { IsAlive = true, TakesOrders = true }));
        Assert.True(SpeechFacts.Contradicts("anyone need repairs?", new SpeechFacts.Snapshot { IsAlive = true, TakesOrders = true }));
        Assert.False(SpeechFacts.ClaimsFree("gm smith here, taking orders"));
    }
}
