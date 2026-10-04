using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class JevJobOptionsTests
{
    [Fact]
    public void From_CapsToTheScorersBestFew()
    {
        var options = JevJobOptions.From(JevPromptTests.Ranking(12));

        Assert.Equal(JevJobOptions.MaxOptions, options.Count);
        Assert.Equal("miner:Mine:Minoc Place A", options[0].ActionId);
    }

    [Fact]
    public void From_SkipsJobsTheRulesForbid()
    {
        var ranked = new List<ScoredAction>
        {
            Scored("miner:Mine:Minoc Mine", SkillKinds.Mine, 5),
            Scored("miner:Hunt:Graveyard", SkillKinds.Hunt, ActionScorer.IneligibleScore),
            Scored("miner:Rest:Inn", SkillKinds.Rest, 2)
        };

        var options = JevJobOptions.From(new ScoreResult { Ranked = ranked, Winner = ranked[0] });

        Assert.Equal(2, options.Count);
        Assert.DoesNotContain(options, option => option.ActionId.Contains("Hunt"));
    }

    [Fact]
    public void From_KeepsTheBestPlaceForEachKindOfJob()
    {
        var ranked = new List<ScoredAction>
        {
            Scored("miner:Mine:Minoc Mine", SkillKinds.Mine, 5),
            Scored("miner:Mine:Cove Mine", SkillKinds.Mine, 4),
            Scored("miner:Rest:Inn", SkillKinds.Rest, 2)
        };

        var options = JevJobOptions.From(new ScoreResult { Ranked = ranked, Winner = ranked[0] });

        Assert.Equal(2, options.Count);
        Assert.Equal("mine ore", options[0].Key);
        Assert.Equal("miner:Mine:Minoc Mine", options[0].ActionId);
    }

    [Fact]
    public void From_GivesContrastingRubricsToDifferentKindsOfJob()
    {
        var ranked = new List<ScoredAction>
        {
            Scored("miner:Mine:Minoc Mine", SkillKinds.Mine, 5),
            Scored("miner:Rest:Inn", SkillKinds.Rest, 2)
        };

        var options = JevJobOptions.From(new ScoreResult { Ranked = ranked, Winner = ranked[0] });

        Assert.Equal("rest", options[1].Key);
        Assert.NotEqual(options[0].What, options[1].What);
        Assert.NotEqual(options[0].NotFor, options[1].NotFor);
        Assert.Contains("wounds", options[0].NotFor);
        Assert.Contains("unhurt", options[1].NotFor);
    }

    [Fact]
    public void From_TwoJobsOfOneGroupReadDifferently()
    {
        var ranked = new List<ScoredAction>
        {
            Scored("pk:Conflict:Britain", SkillKinds.Conflict, 5),
            Scored("pk:Hunt:Britain Woods", SkillKinds.Hunt, 4)
        };

        var options = JevJobOptions.From(new ScoreResult { Ranked = ranked, Winner = ranked[0] });

        Assert.Contains("players", options[0].What);
        Assert.Contains("monsters", options[1].What);
        Assert.Equal(options[0].NotFor, options[1].NotFor);
    }

    [Fact]
    public void Criteria_HoldsWhatAndNotForPerOption()
    {
        var criteria = JevJobOptions.Criteria([new JobOption("rest", "miner:Rest:Inn", "recover", "unhurt")]);

        Assert.Equal("recover; wrong when unhurt", criteria["rest"]);
    }

    private static ScoredAction Scored(string id, string kind, double score) =>
        new(new ActionId(id), kind, "miner", score, "ok");
}
