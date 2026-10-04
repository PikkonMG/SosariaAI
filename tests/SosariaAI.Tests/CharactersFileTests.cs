using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Json;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CharactersFileTests
{
    /// <summary>One in ten of the default 200 people.</summary>
    private const int DefaultFeluccaReds = 20;

    /// <summary>The authored roster a new file writes for each enabled facet.</summary>
    private const int DefaultRosterSize = 13;

    /// <summary>A new file writes one house plot for Felucca and one for Trammel.</summary>
    private const int DefaultHousePlotCount = 2;

    [Fact]
    public void CreateDefault_ReturnsExpectedDefaults()
    {
        var file = CharactersFile.CreateDefault(EraBand.ML);
        var felucca = file.Facets[FacetNames.Felucca];
        var trammel = file.Facets[FacetNames.Trammel];

        Assert.False(file.LogActivity);
        Assert.Equal(CharactersConfiguration.DefaultMusingIntervalMinutes, file.MusingIntervalMinutes);
        Assert.Equal(ThreatRating.DefaultThreatMultiple, file.Career.ThreatMultiple);
        Assert.Equal(CareerSettings.DefaultGoldReserve, file.Career.GoldReserve);
        Assert.Equal(CareerSettings.DefaultVendorSearchRange, file.Career.VendorSearchRange);
        Assert.Equal(CareerSettings.DefaultHouseGold, file.Career.HouseGold);
        Assert.Equal(CareerSettings.DefaultPartyInviteRange, file.Career.PartyInviteRange);
        Assert.Equal(CareerSettings.DefaultLeisureRadius, file.Career.LeisureRadius);
        Assert.Equal(CareerSettings.DefaultLeashRadius, file.Career.LeashRadius);
        Assert.Equal(CareerSettings.DefaultMinThreatToFlee, file.Career.MinThreatToFlee);
        Assert.Equal(CareerSettings.DefaultInviteAnswerSeconds, file.Career.InviteAnswerSeconds);
        Assert.Equal(CareerSettings.DefaultInviteCooldownMinutes, file.Career.InviteCooldownMinutes);
        Assert.Equal(CareerSettings.DefaultDeathAvoidHours, file.Career.DeathAvoidHours);
        Assert.Equal(CareerSettings.DefaultPartyFollowRange, file.Career.PartyFollowRange);
        Assert.NotNull(file.HousePlots);
        Assert.Equal(DefaultHousePlotCount, file.HousePlots.Count);
        AssertDefaultHousePlot(file.HousePlots[0], FacetNames.Felucca);
        AssertDefaultHousePlot(file.HousePlots[1], FacetNames.Trammel);
        Assert.NotNull(file.Nav);
        Assert.False(file.Nav.RebuildOnBoot);
        Assert.Equal(NavSettings.DefaultGateCost, file.Nav.GateCost);
        Assert.Equal(400, file.Nav.GateCost);
        Assert.True(file.Maps[FacetNames.Felucca].Enabled);
        Assert.Equal(CharactersFile.DefaultFeluccaCount, file.Maps[FacetNames.Felucca].Count);
        Assert.Equal(CharactersFile.DefaultFeluccaPkEnabled, file.Maps[FacetNames.Felucca].PkEnabled);
        Assert.Equal(DefaultFeluccaReds, file.Maps[FacetNames.Felucca].PkCount);
        Assert.True(file.Maps[FacetNames.Felucca].PkEnabled);
        Assert.Equal(CharactersFile.DefaultPkEnabled, file.Maps[FacetNames.Trammel].PkEnabled);
        Assert.Equal(CharactersFile.DefaultPkCount, file.Maps[FacetNames.Trammel].PkCount);
        Assert.True(file.Maps[FacetNames.Trammel].Enabled);
        Assert.Equal(CharactersFile.DefaultTrammelCount, file.Maps[FacetNames.Trammel].Count);
        Assert.False(file.Maps[FacetNames.Ilshenar].Enabled);
        Assert.Equal(CharactersFile.DisabledFacetCount, file.Maps[FacetNames.Ilshenar].Count);
        Assert.False(file.Maps[FacetNames.Malas].Enabled);
        Assert.False(file.Maps[FacetNames.Tokuno].Enabled);
        Assert.False(file.Maps[FacetNames.TerMur].Enabled);
        Assert.False(file.Facets.ContainsKey(FacetNames.Ilshenar));
        Assert.Equal(DefaultRosterSize, felucca.Roster.Count);
        Assert.Equal(DefaultRosterSize, trammel.Roster.Count);
        Assert.Equal(PersonasFile.ConnorId, felucca.Roster[0].Id);
        Assert.Equal(PersonasFile.ConnorId, felucca.Roster[0].Persona);
        Assert.Equal(CharactersFile.DefaultReturnAfterDeath, felucca.Roster[0].ReturnAfterDeath);
        Assert.Equal(PersonasFile.MiraId, felucca.Roster[1].Id);
        Assert.Equal(PersonasFile.TobinId, felucca.Roster[2].Id);
        Assert.Equal(PersonasFile.HalId, felucca.Roster[3].Id);
        Assert.Equal(PersonasFile.WrenId, felucca.Roster[4].Id);
        Assert.Contains(SkillKinds.Lumberjack, Names(felucca.Roster[0], "work"));
        Assert.Contains(SkillKinds.VendorSell, Names(felucca.Roster[0], "work"));
        Assert.Contains(SkillKinds.VendorBuy, Names(felucca.Roster[0], "work"));
        Assert.Contains(SkillKinds.Decide, Names(felucca.Roster[0]));
        Assert.Contains(SkillKinds.Tavern, Names(felucca.Roster[0], "tavern"));
        Assert.Contains(SkillKinds.House, Names(felucca.Roster[0], "house"));
        Assert.Contains(SkillKinds.Mine, Names(felucca.Roster[1], "work"));
        Assert.Contains(SkillKinds.VendorSell, Names(felucca.Roster[1], "work"));
        Assert.Contains(SkillKinds.VendorBuy, Names(felucca.Roster[1], "work"));
        Assert.Contains(SkillKinds.Fish, Names(felucca.Roster[2], "work"));
        Assert.Contains(SkillKinds.VendorSell, Names(felucca.Roster[2], "work"));
        Assert.Contains(SkillKinds.VendorBuy, Names(felucca.Roster[2], "work"));
        Assert.Contains(SkillKinds.Patrol, Names(felucca.Roster[3], "work"));
        Assert.Contains(SkillKinds.Lumberjack, Names(felucca.Roster[4], "work"));
        Assert.Equal(PersonasFile.BranId, felucca.Roster[5].Id);
        Assert.Equal(PersonasFile.SelaId, felucca.Roster[6].Id);
        Assert.Equal(PersonasFile.TamId, felucca.Roster[7].Id);
        Assert.Equal(PersonasFile.DunnId, felucca.Roster[8].Id);
        Assert.Equal(PersonasFile.OrlaId, felucca.Roster[9].Id);
        Assert.Equal(PersonasFile.KerrId, felucca.Roster[10].Id);
        Assert.Equal(PersonasFile.NyleId, felucca.Roster[11].Id);
        Assert.Equal(PersonasFile.OsricId, felucca.Roster[12].Id);
        Assert.True(felucca.Areas.ContainsKey(CharactersFile.AreaGraveyard));
        Assert.Equal(CharactersFile.PartyGraveyardCrew, felucca.Parties[0].Id);
        Assert.Contains(PersonasFile.DunnId, felucca.Parties[0].Members);
        Assert.Equal(SkillKinds.Follow, felucca.Roster[8].Routines["graveyard"][0].Skill);
        Assert.Equal(SkillKinds.Decide, felucca.Roster[8].Routines["graveyard"][1].Skill);
        Assert.Equal(SkillKinds.Follow, felucca.Roster[6].Routines["default"][0].Skill);
        Assert.Equal(SkillKinds.Decide, felucca.Roster[6].Routines["default"][1].Skill);
        Assert.Equal(SkillKinds.Hunt, felucca.Roster[5].Routines["graveyard"][0].Skill);
        Assert.Equal(CharactersFile.GraveyardGoPoint, felucca.Roster[5].Routines["graveyard"][0].Target);
        Assert.Equal(CharactersFile.SelaSpawn, felucca.Roster[6].Routines["town"][2].Center);
        Assert.Equal(CharactersFile.TamSpawn, felucca.Roster[7].Routines["town"][2].Center);
        Assert.Equal(CharactersFile.DunnSpawn, felucca.Roster[8].Routines["town"][2].Center);
        Assert.Equal(CharactersFile.OrlaSpawn, felucca.Roster[9].Routines["town"][2].Center);
        Assert.Equal(BuildPresets.Swordsman, felucca.Roster[5].Build.Preset);
        Assert.True(felucca.Roster[5].Build.Veteran);
        Assert.Equal(SkillKinds.UpgradeGear, felucca.Roster[5].Routines["town"][0].Skill);
        Assert.Equal(SkillKinds.Beg, felucca.Roster[5].Routines["town"][1].Skill);
        Assert.Equal(SkillKinds.IdleWander, felucca.Roster[5].Routines["town"][2].Skill);
        Assert.Equal(SkillKinds.Decide, felucca.Roster[5].Routines["town"][3].Skill);
        Assert.Equal(SkillKinds.UpgradeGear, felucca.Roster[6].Routines["town"][0].Skill);
        Assert.Equal(SkillKinds.UpgradeGear, felucca.Roster[8].Routines["town"][0].Skill);
        Assert.Equal(CharactersFile.GraveyardRequiredPower, RequiredPower(felucca.Roster[5], "graveyard"));
        Assert.Equal(CharactersFile.DespiseRequiredPower, RequiredPower(felucca.Roster[5], "despise"));
        Assert.Null(RequiredPower(felucca.Roster[5], "town"));
        Assert.Null(RequiredPower(felucca.Roster[5], "tavern"));
        Assert.Null(RequiredPower(felucca.Roster[6], "town"));
        Assert.Equal(CharactersFile.GraveyardRequiredPower, RequiredPower(felucca.Roster[6], "graveyard"));
        Assert.Equal(CharactersFile.GraveyardRequiredPower, RequiredPower(felucca.Roster[7], "graveyard"));
        Assert.Equal(CharactersFile.GraveyardRequiredPower, RequiredPower(felucca.Roster[8], "graveyard"));
        Assert.Null(RequiredPower(felucca.Roster[8], "town"));
        Assert.Equal(CharactersFile.DespiseRequiredPower, RequiredPower(felucca.Roster[9], "despise"));
        Assert.Equal(CharactersFile.GraveyardRequiredPower, RequiredPower(felucca.Roster[9], "graveyard"));
        Assert.Null(RequiredPower(felucca.Roster[9], "town"));
        Assert.Equal(CharactersFile.KerrSpawn, felucca.Roster[10].Routines["town"][2].Center);
        Assert.Equal(SkillKinds.Track, felucca.Roster[10].Routines["trolls"][0].Skill);
        Assert.Equal(SkillKinds.Anatomy, felucca.Roster[10].Routines["trolls"][1].Skill);
        Assert.Equal(SkillKinds.ArmsLore, felucca.Roster[10].Routines["trolls"][2].Skill);
        Assert.Equal(SkillKinds.Wrestle, felucca.Roster[10].Routines["trolls"][3].Skill);
        Assert.Equal(SkillKinds.Tactics, felucca.Roster[10].Routines["trolls"][4].Skill);
        Assert.Equal(SkillKinds.Parry, felucca.Roster[10].Routines["trolls"][5].Skill);
        Assert.Equal(SkillKinds.Sword, felucca.Roster[10].Routines["trolls"][6].Skill);
        Assert.Equal(SkillKinds.Fence, felucca.Roster[10].Routines["trolls"][7].Skill);
        Assert.Equal(SkillKinds.Archery, felucca.Roster[10].Routines["trolls"][8].Skill);
        Assert.Equal(SkillKinds.Mace, felucca.Roster[10].Routines["trolls"][9].Skill);
        Assert.Equal(SkillKinds.Hunt, felucca.Roster[10].Routines["trolls"][10].Skill);
        Assert.Equal(CharactersFile.TrollGoPoint, felucca.Roster[10].Routines["trolls"][10].Target);
        Assert.Equal(CharactersFile.TrollHuntRequiredPower, RequiredPower(felucca.Roster[10], "trolls"));
        Assert.True(felucca.Areas.ContainsKey(CharactersFile.AreaTrollWoods));
        Assert.Equal(BuildPresets.Swordsman, felucca.Roster[10].Build.Preset);
        Assert.True(felucca.Roster[10].Build.Veteran);
        Assert.Equal(felucca.Roster[0].Spawn, trammel.Roster[0].Spawn);
    }

    [Fact]
    public void DefaultMineArea_ContainsMiraMineApproachAndSpawn()
    {
        var area = new Rectangle2D(
            CharactersFile.DefaultMineAreaX,
            CharactersFile.DefaultMineAreaY,
            CharactersFile.DefaultMineAreaWidth,
            CharactersFile.DefaultMineAreaHeight);

        Assert.True(area.Contains(CharactersFile.MiraMineApproach));
        Assert.True(area.Contains(CharactersFile.MiraSpawn));
    }

    [Fact]
    public void CreateDefault_AlreadyHoldsWhatTheOldBootPatchersAdded()
    {
        var file = CharactersFile.CreateDefault(EraBand.ML);

        foreach (var facet in file.Facets.Values)
        {
            foreach (var character in facet.Roster)
            {
                foreach (var steps in character.Routines.Values)
                {
                    AssertEachFollowedBy(steps, SkillKinds.BankDeposit, SkillKinds.Boat);
                    AssertEachFollowedBy(steps, SkillKinds.VendorSell, SkillKinds.VendorBuy);
                    AssertEachFollowedBy(steps, SkillKinds.Mine, SkillKinds.Smith);
                    AssertEachFollowedBy(steps, SkillKinds.House, SkillKinds.PlayerVendor);
                    AssertEachFollowedBy(steps, SkillKinds.Follow, SkillKinds.Decide);
                    AssertPartyHuntsGatherFirst(steps);
                }
            }

            Assert.Equal(
                [PersonasFile.BranId, PersonasFile.SelaId, PersonasFile.TamId, PersonasFile.DunnId],
                facet.Parties[0].Members
            );
            Assert.NotNull(facet.FindRoster(PersonasFile.KerrId));
            Assert.NotNull(facet.FindRoster(PersonasFile.NyleId));
            Assert.NotNull(facet.FindRoster(PersonasFile.OsricId));
            Assert.True(facet.Areas.ContainsKey(CharactersFile.AreaTrollWoods));
        }
    }

    [Fact]
    public void LoadOrCreate_FirstBootMatchesTheNextBoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-boots-{Guid.NewGuid():N}.json");

        try
        {
            var firstBoot = CharactersFile.LoadOrCreate(path);
            var nextBoot = CharactersFile.LoadOrCreate(path);

            Assert.Equal(JsonConfig.Serialize(firstBoot), JsonConfig.Serialize(nextBoot));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void JsonConfig_RoundTripsEverySkillKind()
    {
        var original = new CharactersConfiguration
        {
            LogActivity = true,
            Facets = OneRoster(
                new CharacterDefinition
                {
                    Id = "connor",
                    Name = "Connor",
                    Persona = "connor",
                    ReturnAfterDeath = TimeSpan.FromMinutes(5),
                    Spawn = new Point3D(1, 2, 3),
                    Routine =
                    [
                        new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = new Point3D(4, 5, 6), Range = 2 },
                        new SkillStepDefinition
                        {
                            Skill = SkillKinds.Lumberjack,
                            Area = new AreaDefinition { X = 10, Y = 20, Width = 30, Height = 40 },
                            FillFraction = 0.75
                        },
                        new SkillStepDefinition { Skill = SkillKinds.BankDeposit, BankSpot = new Point3D(7, 8, 9) },
                        new SkillStepDefinition
                        {
                            Skill = SkillKinds.IdleWander,
                            Center = new Point3D(11, 12, 13),
                            Radius = 4,
                            Duration = TimeSpan.FromMinutes(2)
                        },
                        new SkillStepDefinition
                        {
                            Skill = SkillKinds.Patrol,
                            Points = [new Point3D(1, 1, 0), new Point3D(2, 2, 0)]
                        },
                        new SkillStepDefinition
                        {
                            Skill = SkillKinds.Mine,
                            Area = new AreaDefinition { X = 1, Y = 2, Width = 3, Height = 4 },
                            FillFraction = 0.5
                        },
                        new SkillStepDefinition
                        {
                            Skill = SkillKinds.Fish,
                            Area = new AreaDefinition { X = 5, Y = 6, Width = 7, Height = 8 }
                        }
                    ]
                }
            )
        };

        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-roundtrip-{Guid.NewGuid():N}.json");

        try
        {
            JsonConfig.Serialize(path, original);
            var loaded = JsonConfig.Deserialize<CharactersConfiguration>(path);

            Assert.True(loaded.LogActivity);
            var roster = loaded.Facets[FacetNames.Felucca].Roster;
            Assert.Single(roster);
            var character = roster[0];
            Assert.Equal("connor", character.Id);
            Assert.Equal("Connor", character.Name);
            Assert.Equal("connor", character.Persona);
            Assert.Equal(TimeSpan.FromMinutes(5), character.ReturnAfterDeath);
            Assert.Equal(new Point3D(1, 2, 3), character.Spawn);
            Assert.Equal(7, character.Routine.Count);

            Assert.Equal(SkillKinds.GoTo, character.Routine[0].Skill);
            Assert.Equal(new Point3D(4, 5, 6), character.Routine[0].Target);
            Assert.Equal(2, character.Routine[0].Range);

            Assert.Equal(SkillKinds.Lumberjack, character.Routine[1].Skill);
            Assert.Equal(10, character.Routine[1].Area.X);
            Assert.Equal(20, character.Routine[1].Area.Y);
            Assert.Equal(30, character.Routine[1].Area.Width);
            Assert.Equal(40, character.Routine[1].Area.Height);
            Assert.Equal(0.75, character.Routine[1].FillFraction);

            Assert.Equal(SkillKinds.BankDeposit, character.Routine[2].Skill);
            Assert.Equal(new Point3D(7, 8, 9), character.Routine[2].BankSpot);

            Assert.Equal(SkillKinds.IdleWander, character.Routine[3].Skill);
            Assert.Equal(new Point3D(11, 12, 13), character.Routine[3].Center);
            Assert.Equal(4, character.Routine[3].Radius);
            Assert.Equal(TimeSpan.FromMinutes(2), character.Routine[3].Duration);

            Assert.Equal(SkillKinds.Patrol, character.Routine[4].Skill);
            Assert.Equal([new Point3D(1, 1, 0), new Point3D(2, 2, 0)], character.Routine[4].Points);

            Assert.Equal(SkillKinds.Mine, character.Routine[5].Skill);
            Assert.Equal(1, character.Routine[5].Area.X);
            Assert.Equal(0.5, character.Routine[5].FillFraction);

            Assert.Equal(SkillKinds.Fish, character.Routine[6].Skill);
            Assert.Equal(5, character.Routine[6].Area.X);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void JsonConfig_RoundTripsNamedRoutinesAndSingleRoutine()
    {
        var named = new CharactersConfiguration
        {
            Facets = OneRoster(
                new CharacterDefinition
                {
                    Id = "bran",
                    Persona = "bran",
                    Spawn = new Point3D(1, 2, 3),
                    Build = new BuildDefinition { Preset = BuildPresets.Swordsman, Veteran = true },
                    Routines = new()
                    {
                        ["default"] = [new SkillStepDefinition { Skill = SkillKinds.Decide }],
                        ["graveyard"] =
                        [
                            new SkillStepDefinition
                            {
                                Skill = SkillKinds.Hunt,
                                Area = AreaDefinition.FromName(CharactersFile.AreaGraveyard),
                                Minutes = 15
                            }
                        ]
                    },
                    Choices = [new ChoiceDefinition { Routine = "graveyard", Weight = 3, Description = "hunt" }]
                }
            )
        };

        var single = new CharactersConfiguration
        {
            Facets = OneRoster(
                new CharacterDefinition
                {
                    Id = "connor",
                    Persona = "connor",
                    Spawn = new Point3D(4, 5, 6),
                    Routine = [new SkillStepDefinition { Skill = SkillKinds.IdleWander, Center = new Point3D(4, 5, 6) }]
                }
            )
        };

        var namedPath = Path.Combine(Path.GetTempPath(), $"sosariaai-named-{Guid.NewGuid():N}.json");
        var singlePath = Path.Combine(Path.GetTempPath(), $"sosariaai-single-{Guid.NewGuid():N}.json");

        try
        {
            JsonConfig.Serialize(namedPath, named);
            var loadedNamed = JsonConfig.Deserialize<CharactersConfiguration>(namedPath).Facets[FacetNames.Felucca].Roster[0];
            Assert.Equal("graveyard", loadedNamed.Routines["graveyard"][0].Area.Name);
            Assert.Equal(15, loadedNamed.Routines["graveyard"][0].Minutes);
            Assert.Equal("graveyard", loadedNamed.Choices[0].Routine);

            JsonConfig.Serialize(singlePath, single);
            var loadedSingle = JsonConfig.Deserialize<CharactersConfiguration>(singlePath).Facets[FacetNames.Felucca].Roster[0];
            Assert.Equal(SkillKinds.IdleWander, loadedSingle.ResolvedRoutines()[DecideChoice.DefaultRoutineId][0].Skill);
        }
        finally
        {
            if (File.Exists(namedPath))
            {
                File.Delete(namedPath);
            }

            if (File.Exists(singlePath))
            {
                File.Delete(singlePath);
            }
        }
    }

    [Fact]
    public void JsonConfig_RoundTripsMapsAndFacets()
    {
        var original = CharactersFile.CreateDefault(EraBand.ML);
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-facets-{Guid.NewGuid():N}.json");

        try
        {
            JsonConfig.Serialize(path, original);
            var json = File.ReadAllText(path);
            Assert.Contains("\"nav\"", json);
            Assert.Contains("\"rebuildOnBoot\"", json);
            Assert.Contains("\"gateCost\"", json);

            var loaded = JsonConfig.Deserialize<CharactersConfiguration>(path);
            loaded.Normalize();

            Assert.Equal(ThreatRating.DefaultThreatMultiple, loaded.Career.ThreatMultiple);
            Assert.Equal(CareerSettings.DefaultGoldReserve, loaded.Career.GoldReserve);
            Assert.Equal(CareerSettings.DefaultVendorSearchRange, loaded.Career.VendorSearchRange);
            Assert.Equal(DefaultHousePlotCount, loaded.HousePlots.Count);
            AssertDefaultHousePlot(loaded.HousePlots[0], FacetNames.Felucca);
            AssertDefaultHousePlot(loaded.HousePlots[1], FacetNames.Trammel);
            Assert.NotNull(loaded.Nav);
            Assert.False(loaded.Nav.RebuildOnBoot);
            Assert.Equal(400, loaded.Nav.GateCost);
            Assert.True(loaded.Maps[FacetNames.Felucca].Enabled);
            Assert.Equal(CharactersFile.DefaultFeluccaCount, loaded.Maps[FacetNames.Felucca].Count);
            Assert.True(loaded.Maps[FacetNames.Trammel].Enabled);
            Assert.Equal(CharactersFile.DefaultTrammelCount, loaded.Maps[FacetNames.Trammel].Count);
            Assert.False(loaded.Maps[FacetNames.Ilshenar].Enabled);
            Assert.Equal(DefaultRosterSize, loaded.Facets[FacetNames.Felucca].Roster.Count);
            Assert.Equal(PersonasFile.ConnorId, loaded.Facets[FacetNames.Felucca].Roster[0].Id);
            Assert.Equal(PersonasFile.OrlaId, loaded.Facets[FacetNames.Trammel].Roster[9].Id);
            Assert.Equal(
                CharactersFile.PartyGraveyardCrew,
                loaded.Facets[FacetNames.Felucca].Parties[0].Id
            );
            Assert.DoesNotContain("\"routes\"", json);
            Assert.DoesNotContain("\"route\"", json);
            Assert.DoesNotContain("\"via\"", json);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void OldFile_WithHandMadeRoutes_LoadsAndTravelsByTheGraph()
    {
        const string oldFile = """
            {
              "maps": { "Felucca": { "enabled": true, "count": 1 } },
              "facets": {
                "Felucca": {
                  "routes": { "bank-to-graveyard": [ { "x": 1411, "y": 1665, "z": 10 } ] },
                  "areas": { "graveyard": { "x": 1333, "y": 1441, "width": 84, "height": 82 } },
                  "roster": [
                    {
                      "id": "bran",
                      "routines": {
                        "graveyard": [
                          { "skill": "Hunt", "target": { "x": 1384, "y": 1492, "z": 10 }, "area": "graveyard", "route": "bank-to-graveyard" },
                          { "skill": "GoTo", "target": { "x": 1425, "y": 1695, "z": 0 }, "route": "bank-to-graveyard", "reverse": true },
                          { "skill": "GoTo", "target": { "x": 1444, "y": 1510, "z": 40 }, "via": [ { "x": 1414, "y": 1684, "z": 0 } ] }
                        ]
                      }
                    }
                  ]
                }
              }
            }
            """;
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-old-routes-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, oldFile);
            var loaded = JsonConfig.Deserialize<CharactersConfiguration>(path);
            loaded.Normalize();
            var facet = loaded.Facets[FacetNames.Felucca];
            var steps = facet.Roster[0].Routines["graveyard"];

            Assert.IsType<HuntSkill>(SkillFactory.Create(steps[0], facet, FacetNames.Felucca));
            var back = Assert.IsType<TravelSkill>(SkillFactory.Create(steps[1], facet, FacetNames.Felucca));
            Assert.Equal(new Point3D(1425, 1695, 0), back.Goal);
            var up = Assert.IsType<TravelSkill>(SkillFactory.Create(steps[2], facet, FacetNames.Felucca));
            Assert.Equal(new Point3D(1444, 1510, 40), up.Goal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void AssertDefaultHousePlot(HousePlot plot, string map)
    {
        Assert.NotNull(plot);
        Assert.Equal(map, plot.Map);
        Assert.Equal(HouseRules.DefaultPlotX, plot.X);
        Assert.Equal(HouseRules.DefaultPlotY, plot.Y);
        Assert.Equal(HouseRules.DefaultPlotZ, plot.Z);
    }

    private static Dictionary<string, FacetContent> OneRoster(CharacterDefinition character) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [FacetNames.Felucca] = new FacetContent { Roster = [character] }
        };

    private static void AssertEachFollowedBy(List<SkillStepDefinition> steps, string kind, string next)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Skill == kind)
            {
                Assert.True(i + 1 < steps.Count && steps[i + 1].Skill == next, $"{kind} is not followed by {next}");
            }
        }
    }

    // A party hunt names its own meeting spot; no walk goes ahead of it.
    private static void AssertPartyHuntsGatherFirst(List<SkillStepDefinition> steps)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Skill != SkillKinds.Hunt || string.IsNullOrWhiteSpace(steps[i].Party))
            {
                continue;
            }

            Assert.NotEqual(Point3D.Zero, steps[i].Target);
            Assert.False(i > 0 && steps[i - 1].Skill == SkillKinds.GoTo, "a walk goes ahead of a party hunt");
        }
    }

    private static int? RequiredPower(CharacterDefinition character, string routine)
    {
        var choices = character.Choices;

        if (choices == null)
        {
            return null;
        }

        for (var i = 0; i < choices.Count; i++)
        {
            if (string.Equals(choices[i].Routine, routine, StringComparison.OrdinalIgnoreCase))
            {
                return choices[i].RequiredPower;
            }
        }

        return null;
    }

    private static string[] Names(CharacterDefinition character, string routine = null)
    {
        var steps = character.ResolvedRoutines()[routine ?? DecideChoice.DefaultRoutineId];
        var names = new string[steps.Count];

        for (var i = 0; i < steps.Count; i++)
        {
            names[i] = steps[i].Skill;
        }

        return names;
    }

    [Theory]
    [InlineData(EraBand.T2A, false)]
    [InlineData(EraBand.ML, true)]
    [InlineData(EraBand.Modern, true)]
    public void CreateDefault_TurnsOnTheFacetsOfTheEra(EraBand band, bool trammelOn)
    {
        var maps = CharactersFile.CreateDefault(band).Maps;

        Assert.True(maps[FacetNames.Felucca].Enabled);
        Assert.Equal(trammelOn, maps[FacetNames.Trammel].Enabled);
        Assert.Equal(CharactersFile.DefaultTrammelCount, maps[FacetNames.Trammel].Count);
        Assert.False(maps[FacetNames.Ilshenar].Enabled);
        Assert.False(maps[FacetNames.Malas].Enabled);
        Assert.False(maps[FacetNames.Tokuno].Enabled);
        Assert.False(maps[FacetNames.TerMur].Enabled);
    }

    [Theory]
    [InlineData(EraBand.T2A)]
    [InlineData(EraBand.ML)]
    [InlineData(EraBand.Modern)]
    public void DefaultFacetOn_NeverTurnsOnAFacetWithoutANavGraph(EraBand band)
    {
        Assert.True(CharactersFile.DefaultFacetOn(FacetNames.Felucca, band));
        Assert.Equal(band != EraBand.T2A, CharactersFile.DefaultFacetOn(FacetNames.Trammel, band));
        Assert.False(CharactersFile.DefaultFacetOn(FacetNames.Ilshenar, band));
        Assert.False(CharactersFile.DefaultFacetOn(FacetNames.Malas, band));
        Assert.False(CharactersFile.DefaultFacetOn(FacetNames.Tokuno, band));
        Assert.False(CharactersFile.DefaultFacetOn(FacetNames.TerMur, band));
        Assert.False(CharactersFile.DefaultFacetOn("Atlantis", band));
    }

    [Theory]
    [InlineData(EraBand.T2A, Expansion.T2A)]
    [InlineData(EraBand.ML, Expansion.AOS)]
    [InlineData(EraBand.Modern, Expansion.SA)]
    public void DefaultFacets_AreAllowedInEveryExpansionOfTheirBand(EraBand band, Expansion first)
    {
        foreach (var (facet, toggle) in CharactersFile.CreateDefault(band).Maps)
        {
            Assert.True(toggle is not { Enabled: true } || EraRules.FacetAllowed(facet, first), facet);
        }
    }

    [Fact]
    public void LoadOrCreate_InvalidJson_UsesDefaultsInsteadOfThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-badchars-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, "{ \"logActivity\": true, \"maps\": { \"Felucca\": { \"enabled\": yes } } }");

            var loaded = CharactersFile.LoadOrCreate(path);

            Assert.NotNull(loaded);
            Assert.False(loaded.LogActivity);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
