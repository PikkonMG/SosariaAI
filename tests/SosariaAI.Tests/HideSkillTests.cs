using System;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A hider in a region that forbids the skill, or with a spell in its hands, was refused on
/// every think and waited for ever.
/// </summary>
public class HideSkillTests
{
    private static readonly DateTime Start = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GivesUp_WaitsOutTheSkillDelay() =>
        Assert.False(HideSkill.GivesUp(Start, Start + HideSkill.TryLimit - TimeSpan.FromSeconds(1)));

    [Fact]
    public void GivesUp_PastTheLimit() =>
        Assert.True(HideSkill.GivesUp(Start, Start + HideSkill.TryLimit));

    [Fact]
    public void GivesUp_NeverBeforeItStarted() =>
        Assert.False(HideSkill.GivesUp(default, Start + HideSkill.TryLimit));
}
