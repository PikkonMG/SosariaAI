using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TrackRulesTests
{
    [Fact]
    public void TrackSkill_Name_IsTrack() =>
        Assert.Equal(SkillKinds.Track, new TrackSkill().Name);

    [Fact]
    public void RangeFor_GrowsTenTilesPerTenSkill()
    {
        Assert.Equal(10, TrackRules.RangeFor(0));
        Assert.Equal(10, TrackRules.RangeFor(9));
        Assert.Equal(20, TrackRules.RangeFor(10));
        Assert.Equal(60, TrackRules.RangeFor(50));
    }

    [Fact]
    public void PracticeWindow_MatchesModernUOTrackingChecks()
    {
        Assert.Equal(0, TrackRules.PracticeMin);
        Assert.Equal(21.1, TrackRules.FirstCheckMax);
        Assert.Equal(21.1, TrackRules.SecondCheckMin);
        Assert.Equal(100, TrackRules.PracticeMax);
    }

    [Fact]
    public void TrackSkill_Begin_NoTracker_IsFalse() =>
        Assert.False(new TrackSkill().Begin(null));
}
