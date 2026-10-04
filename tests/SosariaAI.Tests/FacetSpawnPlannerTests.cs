using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class FacetSpawnPlannerTests
{
    [Fact]
    public void Plan_UnknownFacetName_IsSkipped()
    {
        var config = new CharactersConfiguration
        {
            Maps = new()
            {
                ["Atlantis"] = new MapToggle { Enabled = true, Count = 3 }
            },
            Facets = new()
        };
        var warnings = new List<FacetPlanWarning>();

        var requests = FacetSpawnPlanner.Plan(config, warnings, Expansion.EJ);

        Assert.Empty(requests);
        Assert.Single(warnings);
        Assert.Equal(FacetSpawnPlanner.UnknownFacetTemplate, warnings[0].Template);
        Assert.Equal("Atlantis", warnings[0].Facet);
    }

    [Fact]
    public void Plan_EnabledFacetWithNoContent_IsSkipped()
    {
        var config = new CharactersConfiguration
        {
            Maps = new()
            {
                [FacetNames.Ilshenar] = new MapToggle { Enabled = true, Count = 4 }
            },
            Facets = new()
        };
        var warnings = new List<FacetPlanWarning>();

        var requests = FacetSpawnPlanner.Plan(config, warnings, Expansion.EJ);

        Assert.Empty(requests);
        Assert.Single(warnings);
        Assert.Equal(FacetSpawnPlanner.MissingContentTemplate, warnings[0].Template);
        Assert.Equal(FacetNames.Ilshenar, warnings[0].Facet);
    }

    [Fact]
    public void Plan_EnabledFacetWithEmptyRoster_IsSkipped()
    {
        var config = new CharactersConfiguration
        {
            Maps = new()
            {
                [FacetNames.Malas] = new MapToggle { Enabled = true, Count = 2 }
            },
            Facets = new()
            {
                [FacetNames.Malas] = new FacetContent { Roster = [] }
            }
        };
        var warnings = new List<FacetPlanWarning>();

        var requests = FacetSpawnPlanner.Plan(config, warnings, Expansion.EJ);

        Assert.Empty(requests);
        Assert.Single(warnings);
        Assert.Equal(FacetSpawnPlanner.MissingContentTemplate, warnings[0].Template);
        Assert.Equal(FacetNames.Malas, warnings[0].Facet);
    }

    [Fact]
    public void Plan_EnabledFacetWithRoster_BuildsSpawnPlan()
    {
        var config = new CharactersConfiguration
        {
            Maps = new()
            {
                [FacetNames.Felucca] = new MapToggle { Enabled = true, Count = 2 }
            },
            Facets = new()
            {
                [FacetNames.Felucca] = new FacetContent
                {
                    Roster =
                    [
                        new CharacterDefinition { Id = "connor" },
                        new CharacterDefinition { Id = "mira" }
                    ]
                }
            }
        };

        var requests = FacetSpawnPlanner.Plan(config, new List<FacetPlanWarning>(), Expansion.EJ);

        Assert.Single(requests);
        Assert.Equal(FacetNames.Felucca, requests[0].Facet);
        Assert.Equal(2, requests[0].Entries.Count);
        Assert.Equal("connor", requests[0].Entries[0].UniqueId);
        Assert.Equal("mira", requests[0].Entries[1].UniqueId);
    }

    [Fact]
    public void Plan_T2A_SkipsTrammelEvenWhenEnabled()
    {
        var config = new CharactersConfiguration
        {
            Maps = new()
            {
                [FacetNames.Felucca] = new MapToggle { Enabled = true, Count = 1 },
                [FacetNames.Trammel] = new MapToggle { Enabled = true, Count = 1 }
            },
            Facets = new()
            {
                [FacetNames.Felucca] = new FacetContent
                {
                    Roster = [new CharacterDefinition { Id = "connor" }]
                },
                [FacetNames.Trammel] = new FacetContent
                {
                    Roster = [new CharacterDefinition { Id = "stockard" }]
                }
            }
        };
        var warnings = new List<FacetPlanWarning>();

        var requests = FacetSpawnPlanner.Plan(config, warnings, Expansion.T2A);

        Assert.Single(requests);
        Assert.Equal(FacetNames.Felucca, requests[0].Facet);
        Assert.Contains(warnings, w =>
            w.Template == FacetSpawnPlanner.EraSkippedTemplate && w.Facet == FacetNames.Trammel);
    }

    [Fact]
    public void Plan_CountBelowTheRoster_KeepsTheOperatorCount_AndWarns()
    {
        const int operatorCount = 1;
        var config = new CharactersConfiguration
        {
            Maps = new()
            {
                [FacetNames.Felucca] = new MapToggle { Enabled = true, Count = operatorCount }
            },
            Facets = new()
            {
                [FacetNames.Felucca] = new FacetContent
                {
                    Roster =
                    [
                        new CharacterDefinition { Id = "connor" },
                        new CharacterDefinition { Id = "mira" }
                    ]
                }
            }
        };
        var warnings = new List<FacetPlanWarning>();

        var requests = FacetSpawnPlanner.Plan(config, warnings, Expansion.EJ);

        Assert.Single(Assert.Single(requests).Entries);
        Assert.Equal(
            new FacetPlanWarning(FacetSpawnPlanner.ShortRosterTemplate, FacetNames.Felucca, operatorCount, config.Facets[FacetNames.Felucca].Roster.Count),
            Assert.Single(warnings)
        );
    }
}
