using System;
using System.Collections.Generic;
using Server;
using Server.Guilds;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Spawning;

public readonly record struct WorkSite(string Name, Point3D Home, Rectangle2D Harvest);

/// <summary>
/// Copies of a job do not all work the Britain west wood. Fixtures keep the authored
/// tile. Copies pick a town by a stable id hash, and the town's bank street is home.
/// Harvest boxes stay next to those towns so a worker is not sent into orc woods.
/// </summary>
public static class WorkSites
{
    public const int PatchSize = 24;
    public const int BankScatterRadius = 4;
    public const int TownScatterRadius = 8;

    private const string AuthoredSiteName = "authored";

    public static readonly Point3D BritainGate = new(1336, 1997, 5);
    public static readonly Point3D YewGate = new(771, 752, 5);
    public static readonly Point3D MinocGate = new(2701, 692, 5);
    public static readonly Point3D TrinsicGate = new(1828, 2948, -20);
    public static readonly Point3D SkaraGate = new(643, 2067, 5);
    public static readonly Point3D MoonglowGate = new(4467, 1283, 5);
    public static readonly Point3D JhelomGate = new(1499, 3771, 5);
    public static readonly Point3D MaginciaGate = new(3563, 2139, 0);
    public static readonly Point3D CoveGate = new(2232, 1199, 5);

    // Town activity belongs at banks and shops. Moongates are travel stops, not
    // town centers. Using gate pads as homes sent every copied routine to the
    // wilderness around a gate, and six people paced the Skara pad all afternoon.
    public static readonly Point3D BritainTown = new(1425, 1690, 0);
    public static readonly Point3D YewTown = new(652, 820, 0);
    public static readonly Point3D MinocTown = new(2503, 552, 0);
    public static readonly Point3D TrinsicTown = new(1813, 2825, 0);
    public static readonly Point3D SkaraTown = new(587, 2146, 0);
    public static readonly Point3D MoonglowTown = new(4471, 1156, 0);
    public static readonly Point3D JhelomTown = new(1317, 3773, 0);
    public static readonly Point3D MaginciaTown = new(3730, 2161, 20);
    // The Vesper docks are planks over water: no one can spawn on them, and the tiles
    // round them are sea. Vesper people live at the bank.
    public static readonly Point3D VesperTown = new(2881, 684, 0);
    // Cove has no bank. Its people meet at the provisioner's door.
    public static readonly Point3D CoveTown = new(2216, 1192, 0);

    public static readonly WorkSite BritainWestWood = new(
        "Britain west wood",
        CharactersFile.DefaultSpawn,
        new Rectangle2D(
            CharactersFile.DefaultForestAreaX,
            CharactersFile.DefaultForestAreaY,
            CharactersFile.DefaultForestAreaWidth,
            CharactersFile.DefaultForestAreaHeight
        )
    );

    public static readonly WorkSite BritainSouthRoad = new(
        "Britain south road",
        BritainTown,
        new Rectangle2D(1290, 1920, 80, 80)
    );

    // Skara Brae is a small island with few trees; its "wood" sat on the bare north tip
    // with no street within a hundred tiles. Yew is the forest town.
    public static readonly WorkSite YewWood = new(
        "Yew wood",
        YewTown,
        new Rectangle2D(540, 860, 100, 80)
    );

    public static readonly WorkSite MaginciaWood = new(
        "Magincia wood",
        MaginciaTown,
        new Rectangle2D(3500, 2100, 80, 80)
    );

    public static readonly WorkSite MinocHills = new(
        "Minoc hills",
        MinocTown,
        new Rectangle2D(2580, 620, 140, 120)
    );

    public static readonly WorkSite CoveMine = new(
        "Cove mine",
        CoveTown,
        new Rectangle2D(2160, 1060, 120, 120)
    );

    public static readonly WorkSite VesperMine = new(
        "Vesper mine",
        VesperTown,
        new Rectangle2D(2800, 660, 120, 100)
    );

    public static readonly WorkSite BritainMine = new(
        "Britain mine",
        CharactersFile.MiraSpawn,
        new Rectangle2D(
            CharactersFile.DefaultMineAreaX,
            CharactersFile.DefaultMineAreaY,
            CharactersFile.DefaultMineAreaWidth,
            CharactersFile.DefaultMineAreaHeight
        )
    );

    public static readonly WorkSite BritainRiver = new(
        "Britain river",
        CharactersFile.TobinShore,
        new Rectangle2D(
            CharactersFile.DefaultFishAreaX,
            CharactersFile.DefaultFishAreaY,
            CharactersFile.DefaultFishAreaWidth,
            CharactersFile.DefaultFishAreaHeight
        )
    );

    public static readonly WorkSite VesperWater = new(
        "Vesper water",
        VesperTown,
        new Rectangle2D(2960, 780, 80, 80)
    );

    public static readonly WorkSite SkaraWater = new(
        "Skara water",
        SkaraTown,
        new Rectangle2D(580, 2180, 80, 80)
    );

    public static WorkSite[] LumberSites { get; } =
    [
        BritainWestWood,
        BritainSouthRoad,
        YewWood,
        MaginciaWood
    ];

    // The Britain mine ledge (z 40) is left to the fixture miner. Copies homed there
    // once failed every trip and piled up on the ledge, so they work these sites.
    public static WorkSite[] MineSites { get; } =
    [
        MinocHills,
        CoveMine,
        VesperMine
    ];

    public static WorkSite[] FishSites { get; } =
    [
        BritainRiver,
        VesperWater,
        SkaraWater
    ];

    public static WorkSite[] TownSites { get; } =
    [
        Town(BritainTown, "Britain bank"),
        Town(YewTown, "Yew bank"),
        Town(MinocTown, "Minoc bank"),
        Town(TrinsicTown, "Trinsic bank"),
        Town(SkaraTown, "Skara bank"),
        Town(MoonglowTown, "Moonglow bank"),
        Town(JhelomTown, "Jhelom bank"),
        Town(MaginciaTown, "Magincia bank")
    ];

    // Britain weighs as much as the other big towns. At four shares it held 147 of 800 people
    // on a fresh world, twice any other town, and its bank was a wall of bodies.
    private static WorkSite[] TownPopulation { get; } =
    [
        Town(BritainTown, "Britain bank"), Town(BritainTown, "Britain bank"),
        Town(MinocTown, "Minoc bank"), Town(MinocTown, "Minoc bank"),
        Town(TrinsicTown, "Trinsic bank"), Town(TrinsicTown, "Trinsic bank"),
        Town(YewTown, "Yew bank"), Town(YewTown, "Yew bank"),
        Town(SkaraTown, "Skara bank"),
        Town(MoonglowTown, "Moonglow bank"), Town(MoonglowTown, "Moonglow bank"),
        Town(MaginciaTown, "Magincia bank"),
        Town(JhelomTown, "Jhelom bank")
    ];

    public static Point3D[] PublicGates { get; } =
    [
        BritainGate, YewGate, MinocGate, TrinsicGate, SkaraGate, MoonglowGate, JhelomGate, MaginciaGate, CoveGate
    ];

    /// <summary>The public moongate nearest a spot. A Skara person walking for the Britain pad as a way home never got there.</summary>
    public static Point3D NearestGate(Point3D from) => NearestGate(from, static _ => true);

    /// <summary>
    /// The public moongate nearest a spot that <paramref name="usable"/> accepts, or the
    /// Britain gate when it accepts none. A red's last way home was the guarded Britain pad
    /// from the Britain graveyard, and the walk refused itself at once.
    /// </summary>
    public static Point3D NearestGate(Point3D from, Func<Point3D, bool> usable)
    {
        var best = BritainGate;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < PublicGates.Length; i++)
        {
            var distance = NavMetric.Chebyshev(from, PublicGates[i]);

            if (distance < bestDistance && usable(PublicGates[i]))
            {
                bestDistance = distance;
                best = PublicGates[i];
            }
        }

        return best;
    }

    /// <summary>
    /// The nearest public gate is already under the character's feet. A walk to it ends
    /// where it begins and reports success, which clears the failure count a real trip
    /// was meant to raise. Miners gated onto the Magincia pad looped on this all day.
    /// </summary>
    public static bool AtNearestGate(Point3D location) => AtNearestGate(location, static _ => true);

    /// <summary><see cref="AtNearestGate(Point3D)"/> for the nearest gate <paramref name="usable"/> accepts.</summary>
    public static bool AtNearestGate(Point3D location, Func<Point3D, bool> usable) =>
        NavMetric.Chebyshev(location, NearestGate(location, usable)) <= GatePad.ReachTiles;

    public static bool IsLegacyGateHome(Point3D home) =>
        home == BritainGate || home == YewGate || home == MinocGate || home == TrinsicGate ||
        home == SkaraGate || home == MoonglowGate || home == JhelomGate || home == MaginciaGate;

    /// <summary>
    /// A saved copy homed on a retired site gets a new home at bind time: the old gate
    /// pads, and the Britain mine ledge.
    /// </summary>
    public static bool NeedsNewCopyHome(Point3D home) =>
        IsLegacyGateHome(home) || home == BritainMine.Home;

    public static bool IsCopy(string uniqueId)
    {
        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            return false;
        }

        return uniqueId.IndexOf(SpawnPlan.DuplicateMark) >= 0;
    }

    public static int StableIndex(string uniqueId)
    {
        if (string.IsNullOrWhiteSpace(uniqueId))
        {
            return 0;
        }

        return (int)(HashUtility.ComputeHash32(uniqueId.ToUpperInvariant()) & int.MaxValue);
    }

    /// <summary>A stable roll per person and purpose: the same id and salt always give
    /// the same number, so a reboot brings back the same pick.</summary>
    public static int StableRoll(string uniqueId, int salt) =>
        (int)((StableIndex(uniqueId) * 2654435761L + salt * 40503L) % int.MaxValue);

    public static Rectangle2D HarvestFor(string skillKind, string uniqueId, Rectangle2D authored)
    {
        var site = SiteFor(skillKind, uniqueId, authored);
        return Patch(site.Harvest, uniqueId);
    }

    public static Point3D Approach(Rectangle2D area, string uniqueId, int z)
    {
        var width = Math.Max(1, area.Width);
        var height = Math.Max(1, area.Height);
        var index = StableIndex(uniqueId);
        var x = area.X + index % width;
        var y = area.Y + index / width % height;
        return new Point3D(x, y, z);
    }

    public static Point3D WalkTarget(Point3D authored, string uniqueId) =>
        WalkTarget(authored, uniqueId, Point3D.Zero);

    /// <param name="homeSpot">
    /// The character's own bank spot. A copy's bank came from a hash over every town, so
    /// a Magincia woodcutter banked in Minoc and never got there.
    /// </param>
    public static Point3D WalkTarget(Point3D authored, string uniqueId, Point3D homeSpot)
    {
        if (authored == Point3D.Zero)
        {
            return authored;
        }

        if (TryWorkKind(authored, out var kind, out var authoredHarvest, out var authoredHome))
        {
            var area = HarvestFor(kind, uniqueId, authoredHarvest);
            return Approach(area, uniqueId, ApproachHeight(authored, uniqueId, HomeFor(authoredHome, uniqueId, kind)));
        }

        if (authored == CharactersFile.DefaultBankSpot)
        {
            if (IsCopy(uniqueId))
            {
                var town = homeSpot != Point3D.Zero ? homeSpot : TownSites[StableIndex(uniqueId) % TownSites.Length].Home;
                return OffPad(town, uniqueId);
            }

            return Scatter(authored, uniqueId, BankScatterRadius);
        }

        return authored;
    }

    /// <summary>
    /// The work patch a walk to <paramref name="authored"/> is sent to, when the spot is a
    /// work spot (<see cref="WalkTarget(Point3D, string, Point3D)"/>).
    /// </summary>
    public static bool TryWalkPatch(Point3D authored, string uniqueId, out Rectangle2D patch)
    {
        if (!TryWorkKind(authored, out var kind, out var authoredHarvest, out _))
        {
            patch = default;
            return false;
        }

        patch = HarvestFor(kind, uniqueId, authoredHarvest);
        return true;
    }

    /// <summary>
    /// The height a work walk aims at. A fixture keeps its authored spot's height. A copy
    /// works another site, so it takes that site's town height: a Cove miner carried the
    /// Britain ledge's 40 into the Cove mine, where the floor lies near 0, and thirteen
    /// walks ended with no tile to stand on within eight tiles of the goal.
    /// </summary>
    public static int ApproachHeight(Point3D authored, string uniqueId, Point3D siteHome) =>
        IsCopy(uniqueId) ? siteHome.Z : authored.Z;

    /// <summary>
    /// A tile inside an authored harvest box. A walk there is read as a trip to work and
    /// sent to the walker's own work patch, so a home corner or an idle spot must not be one.
    /// </summary>
    public static bool IsWorkSpot(Point3D spot) => TryWorkKind(spot, out _, out _, out _);

    public static Point3D OffPad(Point3D gate, string uniqueId)
    {
        var dest = Scatter(gate, uniqueId, TownScatterRadius);

        if (NavMetric.Chebyshev(dest, gate) < GateHopRules.StepOffTiles)
        {
            return GateHopRules.StepOff(gate, dest);
        }

        return dest;
    }

    public static Point3D Scatter(Point3D dest, string uniqueId, int radius)
    {
        if (string.IsNullOrWhiteSpace(uniqueId) || radius <= 0)
        {
            return dest;
        }

        var hash = StableIndex(uniqueId);
        var span = radius * 2 + 1;
        var dx = ((hash % span) + span) % span - radius;
        var dy = (((hash / 17) % span) + span) % span - radius;

        if (dx == 0 && dy == 0)
        {
            dx = 1;
        }

        return new Point3D(dest.X + dx, dest.Y + dy, dest.Z);
    }

    private static bool TryWorkKind(
        Point3D authored,
        out string kind,
        out Rectangle2D authoredHarvest,
        out Point3D authoredHome
    )
    {
        var tile = new Point2D(authored.X, authored.Y);

        if (authored == CharactersFile.DefaultForestApproach ||
            BritainWestWood.Harvest.Contains(tile))
        {
            kind = SkillKinds.Lumberjack;
            authoredHarvest = BritainWestWood.Harvest;
            authoredHome = CharactersFile.DefaultSpawn;
            return true;
        }

        if (authored == CharactersFile.MiraMineApproach ||
            authored == CharactersFile.MiraSpawn ||
            BritainMine.Harvest.Contains(tile))
        {
            kind = SkillKinds.Mine;
            authoredHarvest = BritainMine.Harvest;
            authoredHome = CharactersFile.MiraSpawn;
            return true;
        }

        if (authored == CharactersFile.TobinShore || BritainRiver.Harvest.Contains(tile))
        {
            kind = SkillKinds.Fish;
            authoredHarvest = BritainRiver.Harvest;
            authoredHome = CharactersFile.TobinShore;
            return true;
        }

        kind = SkillKinds.IdleWander;
        authoredHarvest = default;
        authoredHome = Point3D.Zero;
        return false;
    }

    public static Point3D HomeFor(Point3D authored, string uniqueId, string skillKind) =>
        HomeFor(authored, uniqueId, skillKind, GuildType.Regular);

    /// <summary>
    /// The home of a copy: the town of the site its id picks among the sites its side may call
    /// home (<see cref="SideHomes"/>). A fixture keeps its authored spawn.
    /// </summary>
    public static Point3D HomeFor(Point3D authored, string uniqueId, string skillKind, GuildType side)
    {
        if (!IsCopy(uniqueId))
        {
            return authored;
        }

        var sites = SideHomes.For(side, KindSites(skillKind));
        var home = sites.Count == 0 ? Point3D.Zero : sites[StableIndex(uniqueId) % sites.Count].Home;
        return home == Point3D.Zero ? authored : home;
    }

    /// <summary>
    /// The work a person's home follows: the station trade of a crafter's routine (a crafter
    /// homes in a town that has its station, and digs or cuts its own stock near it), else its
    /// harvest or hunt, else town life.
    /// </summary>
    public static string PrimaryWork(CharacterDefinition definition)
    {
        if (definition == null)
        {
            return SkillKinds.Lumberjack;
        }

        if (CraftCareerRules.CareerOf(definition) is { } career)
        {
            return career;
        }

        foreach (var steps in definition.ResolvedRoutines().Values)
        {
            for (var i = 0; i < (steps?.Count ?? 0); i++)
            {
                if (steps[i]?.Skill is SkillKinds.Lumberjack or SkillKinds.Mine or SkillKinds.Fish
                    or SkillKinds.Hunt or SkillKinds.Dungeon)
                {
                    return steps[i].Skill;
                }
            }
        }

        return SkillKinds.IdleWander;
    }

    public static WorkSite SiteFor(string skillKind, string uniqueId, Rectangle2D authored)
    {
        if (!IsCopy(uniqueId))
        {
            return Authored(authored);
        }

        var sites = KindSites(skillKind);
        return sites[StableIndex(uniqueId) % sites.Count];
    }

    /// <summary>The sites a copy of this work picks from: the weighted town list for town life.</summary>
    private static IReadOnlyList<WorkSite> KindSites(string skillKind) =>
        skillKind == SkillKinds.IdleWander ? TownPopulation : SitesOf(skillKind);

    /// <summary>
    /// The site a harvest works. A crafter digging or cutting its own stock works the patch its
    /// routine names, the one nearest its station town (<see cref="NearestSite"/>); every other
    /// copy works the site its id picks.
    /// </summary>
    public static WorkSite HarvestSite(string skillKind, string uniqueId, Rectangle2D authored, bool ownStock) =>
        ownStock ? Authored(authored) : SiteFor(skillKind, uniqueId, authored);

    /// <summary>
    /// The sites a harvest may work, in the order it tries them: its own site
    /// (<see cref="HarvestSite"/>) first, then, for a copy that does not gather its own stock,
    /// every other site of the harvest whose work box lies within <paramref name="leash"/> of
    /// <paramref name="home"/>, nearest first. Woodcutters of a cut-bare Britain wood failed
    /// "no patch of the site has anything to work" 251 times while the Yew and south road woods
    /// stood full.
    /// </summary>
    public static List<WorkSite> HarvestSites(
        string skillKind,
        string uniqueId,
        Rectangle2D authored,
        bool ownStock,
        Point3D home,
        int leash
    )
    {
        var own = HarvestSite(skillKind, uniqueId, authored, ownStock);
        var sites = new List<WorkSite> { own };

        if (ownStock || !IsCopy(uniqueId))
        {
            return sites;
        }

        var others = new List<(WorkSite Site, int Distance)>();
        var kindSites = SitesOf(skillKind);

        for (var i = 0; i < kindSites.Count; i++)
        {
            var site = kindSites[i];
            var distance = NavMetric.Chebyshev(home, CenterOf(site.Harvest, home.Z));

            if (site.Harvest != own.Harvest && distance <= leash)
            {
                others.Add((site, distance));
            }
        }

        others.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));

        for (var i = 0; i < others.Count; i++)
        {
            sites.Add(others[i].Site);
        }

        return sites;
    }

    /// <summary>
    /// The site of a harvest whose work box lies nearest <paramref name="home"/>: a Minoc smith
    /// mines the Minoc hills, a Yew carpenter cuts the Yew wood.
    /// </summary>
    public static WorkSite NearestSite(string skillKind, Point3D home)
    {
        var sites = SitesOf(skillKind);
        var best = sites[0];
        var bestDistance = int.MaxValue;

        for (var i = 0; i < sites.Count; i++)
        {
            var distance = NavMetric.Chebyshev(home, CenterOf(sites[i].Harvest, home.Z));

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = sites[i];
            }
        }

        return best;
    }

    /// <summary>The middle tile of a work box, at height <paramref name="z"/>.</summary>
    private static Point3D CenterOf(Rectangle2D box, int z) => new(box.X + box.Width / 2, box.Y + box.Height / 2, z);

    private static WorkSite Authored(Rectangle2D authored) =>
        new(AuthoredSiteName, new Point3D(authored.X, authored.Y, 0), authored);

    public static Rectangle2D Patch(Rectangle2D area, string uniqueId) =>
        Patch(area, uniqueId, 0);

    /// <summary>
    /// The hashed cell moved salt cells forward. A work patch the graph cannot
    /// reach rotates to the next cell of the same site instead of failing forever.
    /// The last row and column take what is left of the site, so no strip of it is idle.
    /// </summary>
    public static Rectangle2D Patch(Rectangle2D area, string uniqueId, int salt)
    {
        if (area.Width <= PatchSize && area.Height <= PatchSize)
        {
            return area;
        }

        var index = StableIndex(uniqueId) + salt;
        var cols = CellsAcross(area.Width);
        var rows = CellsAcross(area.Height);
        var cell = index % (cols * rows);
        var col = cell % cols;
        var row = cell / cols;
        var width = Math.Min(PatchSize, area.Width - col * PatchSize);
        var height = Math.Min(PatchSize, area.Height - row * PatchSize);

        if (width <= 0 || height <= 0)
        {
            return area;
        }

        return new Rectangle2D(area.X + col * PatchSize, area.Y + row * PatchSize, width, height);
    }

    /// <summary>How many distinct cells <see cref="Patch"/> can return for an area.</summary>
    public static int PatchCount(Rectangle2D area)
    {
        if (area.Width <= PatchSize && area.Height <= PatchSize)
        {
            return 1;
        }

        return CellsAcross(area.Width) * CellsAcross(area.Height);
    }

    /// <summary>The cells of <see cref="PatchSize"/> a side of this length splits into, the last one short.</summary>
    private static int CellsAcross(int length) => Math.Max(1, (length + PatchSize - 1) / PatchSize);

    private static WorkSite Town(Point3D gate, string name) =>
        new(name, gate, new Rectangle2D(gate.X, gate.Y, 1, 1));

    private static IReadOnlyList<WorkSite> SitesOf(string skillKind) =>
        skillKind switch
        {
            SkillKinds.Lumberjack => LumberSites,
            SkillKinds.Mine => MineSites,
            SkillKinds.Fish => FishSites,
            _ when CraftCareerRules.HomesFor(skillKind) is { Count: > 0 } towns => towns,
            _ => TownSites
        };
}
