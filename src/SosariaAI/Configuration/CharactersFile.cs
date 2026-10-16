using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Json;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Skills;
using SosariaAI.Spawning;

namespace SosariaAI.Configuration;

/// <summary>
/// Default characters. A walk step names only where it goes; the navigation graph plans
/// the way there. A new file has a block for each map the server has: the six stock maps as
/// they always were, and a map of the shard's own with the same roster, whose Britannia spots
/// give way to the map's own places at boot (<see cref="Spawning.WorldAnchors"/>).
/// </summary>
public static class CharactersFile
{
    /// <summary>
    /// Peak Felucca population in a new characters.json (operator approved 2026-09-23).
    /// A file that names a count keeps it.
    /// </summary>
    public const int DefaultFeluccaCount = 200;

    /// <summary>The people of a map of the shard's own in a new file: as many as Felucca's.</summary>
    public const int DefaultCustomMapCount = DefaultFeluccaCount;

    public const int DefaultTrammelCount = 5;
    public const int DisabledFacetCount = 0;
    /// <summary>
    /// A new file turns reds on in Felucca, and on a map of the shard's own with open PvP, and
    /// writes the count out: one in ten of the people (<see cref="OutlawRules.RedShare"/>). Other
    /// facets have no reds. A file that leaves pkEnabled out gets reds on a map with open PvP
    /// (<see cref="FacetMaps.OpenPvp(string)"/>) when it loads; no map without it has reds.
    /// </summary>
    public const bool DefaultOpenPvpPkEnabled = true;
    public const bool DefaultPkEnabled = false;
    public const int DefaultPkCount = 0;

    public static readonly TimeSpan DefaultReturnAfterDeath = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan DefaultIdleDuration = TimeSpan.FromSeconds(90);

    public const double DefaultFillFraction = 0.8;
    public const int DefaultIdleRadius = 6;
    public const string LoiterRoutineId = "loiter";
    public const int LoiterWeight = 1;
    public const int DefaultGoToRange = 1;

    // The street in front of the Britain west bank.
    public static readonly Point3D DefaultBankSpot = new(1425, 1695, 0);

    // Connor and Wren: the street east of the bank and the thin wood across the river bridge.
    public static readonly Point3D DefaultSpawn = new(1445, 1697, 0);
    public static readonly Point3D WrenSpawn = new(1452, 1700, 0);
    public static readonly Point3D DefaultForestApproach = new(1392, 1724, 5);
    public const int DefaultForestAreaX = 1376;
    public const int DefaultForestAreaY = 1708;
    public const int DefaultForestAreaWidth = 40;
    public const int DefaultForestAreaHeight = 40;

    // Mira: the mountain just north of Britain, 131 mountain tiles, 114 reachable on foot. Height 40.
    // Area includes MiraSpawn and MiraMineApproach. Do not shift Y north into troll country.
    public static readonly Point3D MiraSpawn = new(1444, 1510, 40);
    public static readonly Point3D MiraMineApproach = new(1447, 1508, 40);
    public const int DefaultMineAreaX = 1440;
    public const int DefaultMineAreaY = 1500;
    public const int DefaultMineAreaWidth = 60;
    public const int DefaultMineAreaHeight = 50;

    // Tobin: the river bank west of town. The docks are cut off from the bank by the river.
    public static readonly Point3D TobinShore = new(1380, 1722, 2);
    public static readonly Point3D RiverBridge = new(1400, 1708, 20);
    public const int DefaultFishAreaX = 1350;
    public const int DefaultFishAreaY = 1700;
    public const int DefaultFishAreaWidth = 50;
    public const int DefaultFishAreaHeight = 50;

    // Hal: starts at the bank and walks to the river and the blacksmith.
    public static readonly Point3D HalSpawn = new(1430, 1697, 0);
    public static readonly Point3D HalBlacksmith = new(1507, 1579, 20);

    public static readonly Point3D GraveyardGoPoint = new(1384, 1492, 10);
    // The Despise entry hall: the default delve's hall when the catalog picks none.
    public static readonly Point3D DespiseEntryway = new(5587, 631, 30);
    public static readonly Point3D BranSpawn = new(1432, 1696, 0);
    public static readonly Point3D SelaSpawn = new(1434, 1698, 0);
    public static readonly Point3D TamSpawn = new(1436, 1696, 0);
    public static readonly Point3D DunnSpawn = new(1428, 1698, 0);
    public static readonly Point3D OrlaSpawn = new(1422, 1696, 0);
    public static readonly Point3D KerrSpawn = new(1418, 1696, 0);
    public static readonly Point3D NyleSpawn = new(1428, 1694, 0);
    public static readonly Point3D OsricSpawn = new(1368, 1758, 0);
    public static readonly Point3D BritainDock = new(1495, 1768, 0);
    public static readonly Point3D TrollGoPoint = new(1330, 1246, 0);

    public const string AreaGraveyard = "graveyard";
    public const string AreaTrollWoods = "troll-woods";
    public const string PartyGraveyardCrew = "graveyard-crew";

    public const int GraveyardAreaX = 1333;
    public const int GraveyardAreaY = 1441;
    public const int GraveyardAreaWidth = 84;
    public const int GraveyardAreaHeight = 82;
    public const int GraveyardHuntMinutes = 15;
    public const int DespiseHuntMinutes = 20;
    public const int TrollHuntMinutes = 15;
    public const int GraveyardRequiredPower = 40;
    public const int DespiseRequiredPower = 120;
    public const int TrollHuntRequiredPower = 120;
    public const int TrollWoodsAreaX = 1320;
    public const int TrollWoodsAreaY = 1180;
    public const int TrollWoodsAreaWidth = 70;
    public const int TrollWoodsAreaHeight = 90;
    public const int HuntRestMinutes = 2;
    public const int DungeonRestMinutes = 3;

    public const string FileName = "characters.json";

    public static string DefaultPath => ConfigFile.PathIn(FileName);

    public static CharactersConfiguration LoadOrCreate(string path)
    {
        if (File.Exists(path))
        {
            var loaded = ConfigFile.LoadOrDefault<CharactersConfiguration>(path, null, CreateDefault);
            loaded.Normalize();
            LifeRoutineMerge.Apply(loaded);
            return loaded;
        }

        // The file holds the shared life routines once. The config handed back has them
        // folded into each character, as the next boot's read of the file does.
        var created = CreateDefaultFile(EraBands.Current(), FacetMaps.Registered());
        JsonConfig.Serialize(path, created);
        LifeRoutineMerge.Apply(created);
        return created;
    }

    /// <summary>The built-in setup for the running era and the server's maps, with the shared life routines folded into each character.</summary>
    public static CharactersConfiguration CreateDefault() => CreateDefault(EraBands.Current());

    /// <summary>The built-in setup for <paramref name="band"/> and the server's maps, with the shared life routines folded into each character.</summary>
    public static CharactersConfiguration CreateDefault(EraBand band) => CreateDefault(band, FacetMaps.Registered());

    /// <summary>
    /// The built-in setup for <paramref name="band"/> and the maps <paramref name="maps"/>, with
    /// the shared life routines folded into each character. No map at all writes the six stock maps.
    /// </summary>
    public static CharactersConfiguration CreateDefault(EraBand band, IReadOnlyList<MapShape> maps)
    {
        var created = CreateDefaultFile(band, maps);
        LifeRoutineMerge.Apply(created);
        return created;
    }

    /// <summary>
    /// The stock facets a new file turns on in an era. Felucca always; Trammel once the era is
    /// past the Second Age, whose 1999 feel had one world. Ilshenar, Malas, Tokuno and Ter
    /// Mur stay off in every era: no navigation graph ships for them. An operator file
    /// keeps its own choice. A map of the shard's own follows <see cref="CreateDefault(EraBand, IReadOnlyList{MapShape})"/>.
    /// </summary>
    public static bool DefaultFacetOn(string facet, EraBand band) =>
        FacetNames.TryStock(facet, out var name) &&
        name switch
        {
            FacetNames.Felucca => true,
            FacetNames.Trammel => band != EraBand.T2A,
            _ => false
        };

    /// <summary>The six stock maps at their stock sizes, as a server with no map registered stands in for.</summary>
    private static readonly MapShape[] StockShapes =
    [
        new(FacetNames.Felucca, FacetMaps.BritanniaWidth, FacetMaps.BritanniaHeight, OpenPvp: true),
        new(FacetNames.Trammel, FacetMaps.BritanniaWidth, FacetMaps.BritanniaHeight, OpenPvp: false),
        new(FacetNames.Ilshenar, FacetMaps.IlshenarWidth, FacetMaps.IlshenarHeight, OpenPvp: false),
        new(FacetNames.Malas, FacetMaps.MalasWidth, FacetMaps.MalasHeight, OpenPvp: false),
        new(FacetNames.Tokuno, FacetMaps.TokunoSide, FacetMaps.TokunoSide, OpenPvp: false),
        new(FacetNames.TerMur, FacetMaps.TerMurWidth, FacetMaps.TerMurHeight, OpenPvp: false)
    ];

    /// <summary>
    /// The built-in setup as it is written to disk: shared routines held once. Each stock map
    /// the server has, in stock order, is written as it always was. Then each map of the shard's
    /// own gets the Britain roster and, with open PvP, reds; the first one is turned on when no
    /// stock Felucca or Trammel is, so a one-map shard has people on its first boot.
    /// </summary>
    private static CharactersConfiguration CreateDefaultFile(EraBand band, IReadOnlyList<MapShape> maps)
    {
        var shapes = maps is { Count: > 0 } ? maps : StockShapes;
        var file = new CharactersConfiguration
        {
            Career = new(),
            TownScuffles = new(),
            LifeRoutines = LifeRoutineDefaults.Create(),
            HousePlots = [],
            Nav = new(),
            Maps = new(StringComparer.OrdinalIgnoreCase),
            Facets = new(StringComparer.OrdinalIgnoreCase)
        };
        var britanniaOn = false;

        for (var i = 0; i < FacetNames.All.Length; i++)
        {
            var name = FacetNames.All[i];

            if (!HasStockShape(shapes, name))
            {
                continue;
            }

            file.Maps[name] = StockMap(name, band);
            britanniaOn |= file.Maps[name].Enabled;

            if (name is FacetNames.Felucca or FacetNames.Trammel)
            {
                file.HousePlots.Add(GraveyardVendorPlot(name));
                file.HousePlots.Add(DefaultHousePlot(name));
                file.Facets[name] = CreateBritainContent();
            }
        }

        var customOn = !britanniaOn;

        for (var i = 0; i < shapes.Count; i++)
        {
            var shape = shapes[i];

            if (FacetMaps.IsStock(shape.Name, shape.Width, shape.Height) || string.IsNullOrWhiteSpace(shape.Name))
            {
                continue;
            }

            file.Maps[shape.Name] = shape.OpenPvp
                ? RedMap(customOn, DefaultCustomMapCount)
                : QuietMap(customOn, DefaultCustomMapCount);
            file.Facets[shape.Name] = CreateBritainContent();
            customOn = false;
        }

        return file;
    }

    private static bool HasStockShape(IReadOnlyList<MapShape> shapes, string name)
    {
        for (var i = 0; i < shapes.Count; i++)
        {
            if (string.Equals(shapes[i].Name, name, StringComparison.OrdinalIgnoreCase) &&
                FacetMaps.IsStock(shapes[i].Name, shapes[i].Width, shapes[i].Height))
            {
                return true;
            }
        }

        return false;
    }

    private static MapToggle StockMap(string name, EraBand band) =>
        name switch
        {
            FacetNames.Felucca => RedMap(enabled: true, DefaultFeluccaCount),
            FacetNames.Trammel => QuietMap(DefaultFacetOn(FacetNames.Trammel, band), DefaultTrammelCount),
            _ => DisabledMap()
        };

    // A facet without reds. Trammel keeps its count while off, so turning it on is one edit.
    private static MapToggle QuietMap(bool enabled, int count) =>
        new()
        {
            Enabled = enabled,
            Count = count,
            PkEnabled = DefaultPkEnabled,
            PkCount = DefaultPkCount,
            PkGangPercent = PkGangRules.DefaultGangPercent
        };

    private static MapToggle RedMap(bool enabled, int count) =>
        new()
        {
            Enabled = enabled,
            Count = count,
            PkEnabled = DefaultOpenPvpPkEnabled,
            PkCount = OutlawRules.RedCount(DefaultOpenPvpPkEnabled, pkCount: null, count, DefaultOpenPvpPkEnabled),
            PkGangPercent = PkGangRules.DefaultGangPercent
        };

    private static MapToggle DisabledMap() => QuietMap(enabled: false, DisabledFacetCount);

    /// <summary>A crafter's vendor house just outside the Britain graveyard, clear of its yard.</summary>
    private static HousePlot GraveyardVendorPlot(string map) =>
        new()
        {
            Map = map,
            X = HouseRules.GraveyardPlotX,
            Y = HouseRules.GraveyardPlotY,
            Z = HouseRules.GraveyardPlotZ,
            Vendor = true,
            Clear = HouseRules.GraveyardClearTiles,
            Reach = HouseRules.GraveyardReachTiles
        };

    private static HousePlot DefaultHousePlot(string map) =>
        new()
        {
            Map = map,
            X = HouseRules.DefaultPlotX,
            Y = HouseRules.DefaultPlotY,
            Z = HouseRules.DefaultPlotZ
        };

    private static FacetContent CreateBritainContent() =>
        new()
        {
            Areas = new(StringComparer.OrdinalIgnoreCase)
            {
                [AreaGraveyard] = new AreaDefinition
                {
                    X = GraveyardAreaX,
                    Y = GraveyardAreaY,
                    Width = GraveyardAreaWidth,
                    Height = GraveyardAreaHeight
                },
                [AreaTrollWoods] = new AreaDefinition
                {
                    X = TrollWoodsAreaX,
                    Y = TrollWoodsAreaY,
                    Width = TrollWoodsAreaWidth,
                    Height = TrollWoodsAreaHeight
                }
            },
            // The graveyard crew: Bran leads Sela, Tam and Dunn.
            Parties =
            [
                new PartyDefinition
                {
                    Id = PartyGraveyardCrew,
                    Leader = PersonasFile.BranId,
                    Members =
                    [
                        PersonasFile.BranId,
                        PersonasFile.SelaId,
                        PersonasFile.TamId,
                        PersonasFile.DunnId
                    ],
                    MeetAt = DefaultBankSpot
                }
            ],
            Roster =
            [
                Woodcutter(PersonasFile.ConnorId, DefaultSpawn),
                Miner(),
                Fisher(),
                Walker(),
                Woodcutter(PersonasFile.WrenId, WrenSpawn),
                Bran(),
                Sela(),
                Tam(),
                Dunn(),
                Orla(),
                Kerr(),
                Nyle(),
                Osric()
            ]
        };

    private static CharacterDefinition Woodcutter(string id, Point3D spawn) =>
        Worker(
            id,
            spawn,
            [
                new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = DefaultForestApproach },
                new SkillStepDefinition
                {
                    Skill = SkillKinds.Lumberjack,
                    Area = new AreaDefinition
                    {
                        X = DefaultForestAreaX,
                        Y = DefaultForestAreaY,
                        Width = DefaultForestAreaWidth,
                        Height = DefaultForestAreaHeight
                    },
                    FillFraction = DefaultFillFraction
                },
                new SkillStepDefinition { Skill = SkillKinds.Fletch },
                new SkillStepDefinition { Skill = SkillKinds.VendorSell },
                new SkillStepDefinition { Skill = SkillKinds.VendorBuy },
                new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = DefaultBankSpot },
                new SkillStepDefinition { Skill = SkillKinds.BankDeposit, BankSpot = DefaultBankSpot },
                new SkillStepDefinition { Skill = SkillKinds.Boat },
                new SkillStepDefinition { Skill = SkillKinds.Cartography },
                new SkillStepDefinition { Skill = SkillKinds.BankShop, BankSpot = DefaultBankSpot }
            ],
            "cut wood, sell it, and bank the gold"
        );

    private static CharacterDefinition Miner() =>
        Worker(
            PersonasFile.MiraId,
            MiraSpawn,
            [
                new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = MiraMineApproach },
                new SkillStepDefinition
                {
                    Skill = SkillKinds.Mine,
                    Area = new AreaDefinition
                    {
                        X = DefaultMineAreaX,
                        Y = DefaultMineAreaY,
                        Width = DefaultMineAreaWidth,
                        Height = DefaultMineAreaHeight
                    },
                    FillFraction = DefaultFillFraction
                },
                new SkillStepDefinition { Skill = SkillKinds.Smith },
                new SkillStepDefinition { Skill = SkillKinds.VendorSell },
                new SkillStepDefinition { Skill = SkillKinds.VendorBuy },
                new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = DefaultBankSpot },
                new SkillStepDefinition { Skill = SkillKinds.BankDeposit, BankSpot = DefaultBankSpot },
                new SkillStepDefinition { Skill = SkillKinds.Boat },
                new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = MiraSpawn }
            ],
            "mine ore, smelt it, sell ingots, and bank the gold"
        );

    private static CharacterDefinition Fisher() =>
        Worker(
            PersonasFile.TobinId,
            TobinShore,
            [
                new SkillStepDefinition
                {
                    Skill = SkillKinds.Fish,
                    Area = new AreaDefinition
                    {
                        X = DefaultFishAreaX,
                        Y = DefaultFishAreaY,
                        Width = DefaultFishAreaWidth,
                        Height = DefaultFishAreaHeight
                    },
                    FillFraction = DefaultFillFraction
                },
                new SkillStepDefinition { Skill = SkillKinds.VendorSell },
                new SkillStepDefinition { Skill = SkillKinds.VendorBuy },
                new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = DefaultBankSpot },
                new SkillStepDefinition { Skill = SkillKinds.BankDeposit, BankSpot = DefaultBankSpot },
                new SkillStepDefinition { Skill = SkillKinds.Boat },
                new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = TobinShore }
            ],
            "fish, sell the catch, and bank the gold"
        );

    private static CharacterDefinition Walker() =>
        Worker(
            PersonasFile.HalId,
            HalSpawn,
            [new SkillStepDefinition { Skill = SkillKinds.Patrol, Points = WalkerLoop() }],
            "walk the town beat"
        );

    // Bank, out to the river and back, then to the blacksmith; the loop closes at the bank.
    private static List<Point3D> WalkerLoop() =>
        [DefaultBankSpot, RiverBridge, TobinShore, RiverBridge, DefaultBankSpot, HalBlacksmith];

    private static CharacterDefinition Worker(
        string id,
        Point3D spawn,
        List<SkillStepDefinition> work,
        string workDescription
    )
    {
        work.Add(new SkillStepDefinition { Skill = SkillKinds.Decide });
        var routines = new Dictionary<string, List<SkillStepDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = [new SkillStepDefinition { Skill = SkillKinds.Decide }],
            ["work"] = work,
            ["town"] = Town(spawn)
        };
        AddLifeRoutines(routines, spawn);
        return new CharacterDefinition
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(id) ? id : char.ToUpperInvariant(id[0]) + id[1..],
            Persona = id,
            Spawn = spawn,
            ReturnAfterDeath = DefaultReturnAfterDeath,
            Build = WorkerBuild(),
            Routines = routines,
            Choices = LifeChoices(Choice("work", 4, workDescription))
        };
    }

    /// <summary>
    /// Loiter holds the character's own idle centre, so it cannot be shared. Every
    /// other life routine lives once in <see cref="LifeRoutineDefaults"/> and is
    /// folded in by <see cref="LifeRoutineMerge"/> as the file is read.
    /// </summary>
    private static void AddLifeRoutines(
        Dictionary<string, List<SkillStepDefinition>> routines,
        Point3D home
    )
    {
        routines[LoiterRoutineId] =
        [
            new SkillStepDefinition
            {
                Skill = SkillKinds.Loiter,
                Center = home,
                Radius = DefaultIdleRadius,
                Duration = DefaultIdleDuration
            },
            new SkillStepDefinition { Skill = SkillKinds.Decide }
        ];
    }

    private static List<ChoiceDefinition> LifeChoices(params ChoiceDefinition[] extra)
    {
        var choices = new List<ChoiceDefinition>
        {
            Choice(LoiterRoutineId, LoiterWeight, "sit and do nothing")
        };

        if (extra != null)
        {
            choices.AddRange(extra);
        }

        return choices;
    }

    private static BuildDefinition WorkerBuild() =>
        new()
        {
            Style = "melee",
            Role = "worker",
            Veteran = false,
            CanHeal = true
        };

    private static CharacterDefinition Bran() =>
        Fighter(
            PersonasFile.BranId,
            BranSpawn,
            new BuildDefinition { Preset = BuildPresets.Swordsman, Veteran = true },
            LeaderRoutines(),
            LifeChoices(
                Choice(SkillKinds.Hunt, 3, "hunt undead at the graveyard", "graveyard", GraveyardRequiredPower),
                Choice("despise", 1, "lead the crew into Despise", requiredPower: DespiseRequiredPower),
                Choice("town", 2, "rest and wander town")
            )
        );

    private static CharacterDefinition Sela() =>
        Fighter(
            PersonasFile.SelaId,
            SelaSpawn,
            new BuildDefinition { Preset = BuildPresets.Mage, Veteran = true },
            FollowerRoutines(SelaSpawn),
            LifeChoices(
                Choice("graveyard", 2, "hunt undead at the graveyard", requiredPower: GraveyardRequiredPower),
                Choice("mark", 1, "mark a rune"),
                Choice("town", 1, "rest and wander town")
            )
        );

    private static CharacterDefinition Tam() =>
        Fighter(
            PersonasFile.TamId,
            TamSpawn,
            new BuildDefinition { Preset = BuildPresets.Archer, Veteran = false },
            FollowerRoutines(TamSpawn),
            LifeChoices(
                Choice("graveyard", 2, "hunt undead at the graveyard", requiredPower: GraveyardRequiredPower),
                Choice("town", 1, "rest and wander town")
            )
        );

    private static CharacterDefinition Dunn() =>
        Fighter(
            PersonasFile.DunnId,
            DunnSpawn,
            new BuildDefinition { Preset = BuildPresets.Swordsman, Veteran = false },
            FollowerRoutines(DunnSpawn),
            LifeChoices(
                Choice("graveyard", 4, "hunt undead at the graveyard", requiredPower: GraveyardRequiredPower),
                Choice("town", 1, "rest and wander town")
            )
        );

    private static CharacterDefinition Orla() =>
        Fighter(
            PersonasFile.OrlaId,
            OrlaSpawn,
            new BuildDefinition { Preset = BuildPresets.Archer, Veteran = true },
            SoloRoutines(),
            LifeChoices(
                Choice("despise", 2, "hunt the caves of Despise", requiredPower: DespiseRequiredPower),
                Choice("graveyard", 1, "hunt undead at the graveyard", requiredPower: GraveyardRequiredPower),
                Choice("town", 1, "rest and wander town")
            )
        );

    private static CharacterDefinition Kerr() =>
        Fighter(
            PersonasFile.KerrId,
            KerrSpawn,
            new BuildDefinition { Preset = BuildPresets.Swordsman, Veteran = true },
            TrollHunterRoutines(),
            LifeChoices(
                Choice("trolls", 3, "hunt trolls on the Despise road", requiredPower: TrollHuntRequiredPower),
                Choice("town", 1, "rest and wander town")
            )
        );

    private static CharacterDefinition Nyle() =>
        Fighter(
            PersonasFile.NyleId,
            NyleSpawn,
            new BuildDefinition { Preset = BuildPresets.Thief },
            ThiefRoutines(),
            LifeChoices(
                Choice("lift", 3, "work the bank crowd"),
                Choice("town", 1, "rest and watch")
            )
        );

    private static Dictionary<string, List<SkillStepDefinition>> ThiefRoutines()
    {
        var routines = new Dictionary<string, List<SkillStepDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = [new SkillStepDefinition { Skill = SkillKinds.Decide }],
            // The Britain moongate, south of the guarded farms on the road toward Skara Brae.
            ["lift"] =
            [
                new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = WorkSites.BritainGate },
                new SkillStepDefinition { Skill = SkillKinds.Loiter, Center = WorkSites.BritainGate, Minutes = 2 },
                new SkillStepDefinition { Skill = SkillKinds.Hide },
                new SkillStepDefinition { Skill = SkillKinds.Stealth },
                new SkillStepDefinition { Skill = SkillKinds.Steal },
                new SkillStepDefinition { Skill = SkillKinds.Lockpick },
                new SkillStepDefinition { Skill = SkillKinds.RemoveTrap },
                new SkillStepDefinition { Skill = SkillKinds.Tinker },
                new SkillStepDefinition { Skill = SkillKinds.Snoop },
                new SkillStepDefinition { Skill = SkillKinds.DetectHidden },
                new SkillStepDefinition { Skill = SkillKinds.Poison },
                new SkillStepDefinition { Skill = SkillKinds.BankDeposit },
                new SkillStepDefinition { Skill = SkillKinds.Boat },
                new SkillStepDefinition { Skill = SkillKinds.Decide }
            ],
            ["town"] = Town(NyleSpawn)
        };
        AddLifeRoutines(routines, NyleSpawn);
        return routines;
    }

    private static CharacterDefinition Osric() =>
        Fighter(
            PersonasFile.OsricId,
            OsricSpawn,
            new BuildDefinition { Preset = BuildPresets.Tamer },
            TamerRoutines(),
            LifeChoices(
                Choice(TamerLife.TameRoutineId, TamerLife.TameChoiceWeight, TamerLife.TameChoiceDescription),
                Choice(TamerLife.HuntRoutineId, TamerLife.HuntChoiceWeight, TamerLife.HuntChoiceDescription),
                Choice("town", 1, "rest in town")
            )
        );

    private static Dictionary<string, List<SkillStepDefinition>> TamerRoutines()
    {
        var routines = new Dictionary<string, List<SkillStepDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = [new SkillStepDefinition { Skill = SkillKinds.Decide }],
            [TamerLife.TameRoutineId] = TamerLife.TameTrip(),
            [TamerLife.HuntRoutineId] = TamerLife.PetHunt(),
            ["town"] = Town(DefaultSpawn)
        };
        AddLifeRoutines(routines, OsricSpawn);
        return routines;
    }

    private static CharacterDefinition Fighter(
        string id,
        Point3D spawn,
        BuildDefinition build,
        Dictionary<string, List<SkillStepDefinition>> routines,
        List<ChoiceDefinition> choices
    ) =>
        new()
        {
            Id = id,
            Persona = id,
            Spawn = spawn,
            ReturnAfterDeath = DefaultReturnAfterDeath,
            Build = build,
            Routines = routines,
            Choices = choices
        };

    private static Dictionary<string, List<SkillStepDefinition>> LeaderRoutines()
    {
        var routines = new Dictionary<string, List<SkillStepDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = [new SkillStepDefinition { Skill = SkillKinds.Decide }],
            ["graveyard"] = GraveyardTrip(PartyGraveyardCrew),
            ["despise"] = DespiseTrip(PartyGraveyardCrew),
            ["town"] = Town(BranSpawn)
        };
        AddLifeRoutines(routines, BranSpawn);
        return routines;
    }

    private static Dictionary<string, List<SkillStepDefinition>> FollowerRoutines(Point3D home)
    {
        var routines = new Dictionary<string, List<SkillStepDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = FollowThenDecide(),
            ["graveyard"] = FollowThenDecide(),
            ["despise"] = FollowThenDecide(),
            ["town"] = Town(home),
            ["mark"] =
            [
                new SkillStepDefinition { Skill = SkillKinds.Mark },
                new SkillStepDefinition { Skill = SkillKinds.Recall },
                new SkillStepDefinition { Skill = SkillKinds.Gate },
                new SkillStepDefinition { Skill = SkillKinds.Meditate },
                new SkillStepDefinition { Skill = SkillKinds.Spirit },
                new SkillStepDefinition { Skill = SkillKinds.EvalInt },
                new SkillStepDefinition { Skill = SkillKinds.Mage },
                new SkillStepDefinition { Skill = SkillKinds.Necro },
                new SkillStepDefinition { Skill = SkillKinds.Resist },
                new SkillStepDefinition { Skill = SkillKinds.Inscription },
                new SkillStepDefinition { Skill = SkillKinds.Alchemy },
                new SkillStepDefinition { Skill = SkillKinds.Tailor },
                new SkillStepDefinition { Skill = SkillKinds.Carpentry },
                new SkillStepDefinition { Skill = SkillKinds.Decide }
            ]
        };
        AddLifeRoutines(routines, home);
        return routines;
    }

    private static Dictionary<string, List<SkillStepDefinition>> SoloRoutines()
    {
        var routines = new Dictionary<string, List<SkillStepDefinition>>
        {
            ["default"] = [new SkillStepDefinition { Skill = SkillKinds.Decide }],
            ["graveyard"] = GraveyardTrip(party: null),
            ["town"] = Town(OrlaSpawn),
            ["despise"] = DespiseTrip(party: null)
        };
        AddLifeRoutines(routines, OrlaSpawn);
        return routines;
    }

    private static Dictionary<string, List<SkillStepDefinition>> TrollHunterRoutines()
    {
        var routines = new Dictionary<string, List<SkillStepDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = [new SkillStepDefinition { Skill = SkillKinds.Decide }],
            ["trolls"] = TrollTrip(),
            ["town"] = Town(KerrSpawn)
        };
        AddLifeRoutines(routines, KerrSpawn);
        return routines;
    }

    private static List<SkillStepDefinition> TrollTrip() =>
    [
        new SkillStepDefinition { Skill = SkillKinds.Track },
        new SkillStepDefinition { Skill = SkillKinds.Anatomy },
        new SkillStepDefinition { Skill = SkillKinds.ArmsLore },
        new SkillStepDefinition { Skill = SkillKinds.Wrestle },
        new SkillStepDefinition { Skill = SkillKinds.Tactics },
        new SkillStepDefinition { Skill = SkillKinds.Parry },
        new SkillStepDefinition { Skill = SkillKinds.Sword },
        new SkillStepDefinition { Skill = SkillKinds.Fence },
        new SkillStepDefinition { Skill = SkillKinds.Archery },
        new SkillStepDefinition { Skill = SkillKinds.Mace },
        new SkillStepDefinition
        {
            Skill = SkillKinds.Hunt,
            Area = AreaDefinition.FromName(AreaTrollWoods),
            Minutes = TrollHuntMinutes,
            Target = TrollGoPoint
        },
        new SkillStepDefinition { Skill = SkillKinds.ItemId },
        new SkillStepDefinition
        {
            Skill = SkillKinds.GoTo,
            Target = DefaultBankSpot
        },
        BankStep(),
        new SkillStepDefinition { Skill = SkillKinds.Boat },
        new SkillStepDefinition { Skill = SkillKinds.Camp },
        new SkillStepDefinition { Skill = SkillKinds.Rest, Minutes = HuntRestMinutes },
        new SkillStepDefinition { Skill = SkillKinds.Decide }
    ];

    private static List<SkillStepDefinition> FollowThenDecide() =>
    [
        new SkillStepDefinition { Skill = SkillKinds.Follow, Party = PartyGraveyardCrew },
        new SkillStepDefinition { Skill = SkillKinds.Decide }
    ];

    /// <summary>The Britain graveyard hunt, the bank after it and a rest; a copy's hunt moves to its own ground (<see cref="HuntGround.ForHome"/>).</summary>
    public static List<SkillStepDefinition> GraveyardTrip(string party) =>
    [
        new SkillStepDefinition
        {
            Skill = SkillKinds.Hunt,
            Area = AreaDefinition.FromName(AreaGraveyard),
            Minutes = GraveyardHuntMinutes,
            Party = party,
            Target = GraveyardGoPoint
        },
        new SkillStepDefinition
        {
            Skill = SkillKinds.GoTo,
            Target = DefaultBankSpot
        },
        BankStep(),
        new SkillStepDefinition { Skill = SkillKinds.Boat },
        new SkillStepDefinition { Skill = SkillKinds.Rest, Minutes = HuntRestMinutes },
        new SkillStepDefinition { Skill = SkillKinds.Decide }
    ];

    private static List<SkillStepDefinition> DespiseTrip(string party) =>
    [
        new SkillStepDefinition
        {
            Skill = SkillKinds.Dungeon,
            Target = DespiseEntryway,
            Minutes = DespiseHuntMinutes,
            Party = party
        },
        new SkillStepDefinition { Skill = SkillKinds.Forensic },
        new SkillStepDefinition { Skill = SkillKinds.Music },
        new SkillStepDefinition { Skill = SkillKinds.Peace },
        new SkillStepDefinition { Skill = SkillKinds.Discord },
        new SkillStepDefinition { Skill = SkillKinds.Provoke },
        BankStep(),
        new SkillStepDefinition { Skill = SkillKinds.Boat },
        new SkillStepDefinition { Skill = SkillKinds.Rest, Minutes = DungeonRestMinutes },
        new SkillStepDefinition { Skill = SkillKinds.Decide }
    ];

    private static List<SkillStepDefinition> Town(Point3D center) =>
    [
        new SkillStepDefinition { Skill = SkillKinds.UpgradeGear },
        new SkillStepDefinition { Skill = SkillKinds.Beg },
        new SkillStepDefinition
        {
            Skill = SkillKinds.IdleWander,
            Center = center,
            Radius = DefaultIdleRadius,
            Duration = DefaultIdleDuration
        },
        new SkillStepDefinition { Skill = SkillKinds.Decide }
    ];

    private static SkillStepDefinition BankStep() =>
        new() { Skill = SkillKinds.BankDeposit, BankSpot = DefaultBankSpot };

    private static ChoiceDefinition Choice(
        string routine,
        int weight,
        string description,
        string id = null,
        int? requiredPower = null
    ) =>
        new()
        {
            Routine = id ?? routine,
            Weight = weight,
            Description = description,
            RequiredPower = requiredPower
        };
}
