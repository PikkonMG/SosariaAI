using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Spawning;

namespace SosariaAI.Skills;

/// <summary>One craft a worker can live by, how often a worker copy takes it up, and what its day is.</summary>
public readonly record struct CraftCareer(string Kind, int Weight, string Description);

/// <summary>
/// The crafters of the shard. Every worker template gathers or walks a beat, so no copy ever
/// became a tailor, a carpenter, an alchemist or a scribe: a whole run logged "started Tailor"
/// zero times. A worker copy now rolls a craft career from its id: about half stay gatherers
/// (their ore and logs are what the crafters buy), the rest take up one trade, and their
/// gathering and patrol routines give way to one craft routine at the trade's station. A smith,
/// a carpenter and a bowyer keep the harvest that yields their stock in that routine and work it
/// near their station town when nobody sells them any, as 1999 smiths dug their own ore. Each
/// trade homes in the towns that have its station, at a real forge or shop. Pure. No world objects.
/// </summary>
public static class CraftCareerRules
{
    /// <summary>The routine a crafter's day runs.</summary>
    public const string CraftRoutineId = "craft";

    /// <summary>The craft routine's weight among the person's choices, the same as a worker's work routine.</summary>
    public const int CraftChoiceWeight = 4;

    /// <summary>
    /// Share of worker copies that stay gatherers, against the career weights below: half. Three
    /// of the five worker templates mine or chop, so about one pure miner or lumberjack stands
    /// for every two crafters, and the market has sellers.
    /// </summary>
    public const int GathererWeight = 50;

    private const int CareerSalt = 587;

    public static readonly CraftCareer[] Careers =
    [
        new(SkillKinds.Smith, 12, "hammer arms and armour at the forge and sell them"),
        new(SkillKinds.Tailor, 12, "sew clothes and leather at the tailor's and sell them"),
        new(SkillKinds.Carpentry, 7, "work wood at the carpenter's and sell the furniture"),
        new(SkillKinds.Fletch, 6, "make bows and arrows at the bowyer's and sell them"),
        new(SkillKinds.Alchemy, 6, "brew potions at the alchemist's and sell them"),
        new(SkillKinds.Inscription, 4, "write scrolls at the mage shop and sell them"),
        new(SkillKinds.Tinker, 3, "make tools at the tinker's and sell them")
    ];

    /// <summary>
    /// Towns with a working station for each trade: an anvil and a forge for the smith (Yew has
    /// none), a tailor or tanner for the tailor, a carpenter or tinker for wood, a bowyer for
    /// the fletcher, an alchemist for potions, a mage or scribe for scrolls, a tinker's guild.
    /// </summary>
    private static readonly Dictionary<string, WorkSite[]> HomeTowns = new(StringComparer.OrdinalIgnoreCase)
    {
        [SkillKinds.Smith] = Towns(
            (WorkSites.BritainTown, "Britain bank"), (WorkSites.MinocTown, "Minoc bank"), (WorkSites.TrinsicTown, "Trinsic bank"),
            (WorkSites.SkaraTown, "Skara bank"), (WorkSites.MoonglowTown, "Moonglow bank"), (WorkSites.JhelomTown, "Jhelom bank"),
            (WorkSites.VesperTown, "Vesper bank")
        ),
        [SkillKinds.Tailor] = Towns(
            (WorkSites.BritainTown, "Britain bank"), (WorkSites.TrinsicTown, "Trinsic bank"), (WorkSites.SkaraTown, "Skara bank"),
            (WorkSites.MoonglowTown, "Moonglow bank"), (WorkSites.JhelomTown, "Jhelom bank"), (WorkSites.MaginciaTown, "Magincia bank"),
            (WorkSites.MinocTown, "Minoc bank"), (WorkSites.YewTown, "Yew bank")
        ),
        [SkillKinds.Carpentry] = Towns(
            (WorkSites.BritainTown, "Britain bank"), (WorkSites.MinocTown, "Minoc bank"), (WorkSites.YewTown, "Yew bank"),
            (WorkSites.MoonglowTown, "Moonglow bank"), (WorkSites.SkaraTown, "Skara bank"), (WorkSites.JhelomTown, "Jhelom bank"),
            (WorkSites.TrinsicTown, "Trinsic bank"), (WorkSites.VesperTown, "Vesper bank")
        ),
        [SkillKinds.Fletch] = Towns(
            (WorkSites.BritainTown, "Britain bank"), (WorkSites.YewTown, "Yew bank"), (WorkSites.VesperTown, "Vesper bank")
        ),
        [SkillKinds.Alchemy] = Towns(
            (WorkSites.BritainTown, "Britain bank"), (WorkSites.TrinsicTown, "Trinsic bank"), (WorkSites.MoonglowTown, "Moonglow bank"),
            (WorkSites.SkaraTown, "Skara bank"), (WorkSites.MaginciaTown, "Magincia bank"), (WorkSites.VesperTown, "Vesper bank")
        ),
        [SkillKinds.Inscription] = Towns(
            (WorkSites.BritainTown, "Britain bank"), (WorkSites.TrinsicTown, "Trinsic bank"), (WorkSites.MoonglowTown, "Moonglow bank"),
            (WorkSites.SkaraTown, "Skara bank"), (WorkSites.JhelomTown, "Jhelom bank"), (WorkSites.MaginciaTown, "Magincia bank")
        ),
        [SkillKinds.Tinker] = Towns(
            (WorkSites.MinocTown, "Minoc bank"), (WorkSites.TrinsicTown, "Trinsic bank"), (WorkSites.JhelomTown, "Jhelom bank"),
            (WorkSites.MaginciaTown, "Magincia bank"), (WorkSites.VesperTown, "Vesper bank")
        )
    };

    private static readonly Dictionary<string, CraftTrade> TradesByKind = new(StringComparer.OrdinalIgnoreCase)
    {
        [SkillKinds.Smith] = SmithRules.Trade,
        [SkillKinds.Tailor] = TailorRules.Trade,
        [SkillKinds.Carpentry] = CarpentryRules.Trade,
        [SkillKinds.Fletch] = FletchRules.Trade,
        [SkillKinds.Alchemy] = AlchemyRules.Trade,
        [SkillKinds.Inscription] = InscriptionRules.Trade,
        [SkillKinds.Tinker] = TinkerRules.Trade
    };

    /// <summary>
    /// Every trade worked at a station: the careers, and the cooking and map drawing anyone's
    /// routine may hold.
    /// </summary>
    public static readonly IReadOnlyList<CraftTrade> StationTrades = [.. TradesByKind.Values, CookRules.Trade, CartographyRules.Trade];

    /// <summary>The class that lives by each trade.</summary>
    private static readonly (PersonClass Class, string Kind)[] TradeClasses =
    [
        (PersonClass.Smith, SkillKinds.Smith),
        (PersonClass.Tailor, SkillKinds.Tailor),
        (PersonClass.Carpenter, SkillKinds.Carpentry),
        (PersonClass.Bowyer, SkillKinds.Fletch),
        (PersonClass.Alchemist, SkillKinds.Alchemy),
        (PersonClass.Scribe, SkillKinds.Inscription),
        (PersonClass.Tinker, SkillKinds.Tinker)
    ];

    /// <summary>
    /// The trade a worker copy takes up, or null for one that stays a gatherer. Stable per id,
    /// so a reboot brings back the same crafter. The dice mix the id and the salt: a straight
    /// line roll followed the job roll, so every worker copy landed on ten of the fifty gatherer
    /// points, a smith or a tailor, and a live run of 1098 people spawned no miner at all.
    /// </summary>
    public static string RollCareer(string uniqueId)
    {
        var weights = new int[Careers.Length + 1];
        weights[0] = GathererWeight;

        for (var i = 0; i < Careers.Length; i++)
        {
            weights[i + 1] = Careers[i].Weight;
        }

        var pick = PersonDice.Weighted(uniqueId, CareerSalt, weights);
        return pick == 0 ? null : Careers[pick - 1].Kind;
    }

    /// <summary>The trade of a person's craft routine, or null for anyone who lives by none.</summary>
    public static string CareerOf(CharacterDefinition definition)
    {
        if (definition == null || !definition.ResolvedRoutines().TryGetValue(CraftRoutineId, out var steps))
        {
            return null;
        }

        for (var i = 0; i < (steps?.Count ?? 0); i++)
        {
            if (IsCareer(steps[i]?.Skill))
            {
                return steps[i].Skill;
            }
        }

        return null;
    }

    /// <summary>The class that lives by the trade <paramref name="kind"/>, or null for any other skill.</summary>
    public static PersonClass? ClassOf(string kind)
    {
        for (var i = 0; i < TradeClasses.Length; i++)
        {
            if (string.Equals(TradeClasses[i].Kind, kind, StringComparison.OrdinalIgnoreCase))
            {
                return TradeClasses[i].Class;
            }
        }

        return null;
    }

    /// <summary>
    /// Turns a freshly composed worker into a crafter of <paramref name="kind"/>: routines that
    /// gather or walk a beat, and their choices, give way to the craft routine. The template's
    /// routines are never changed; the person gets lists of its own. The craft routine's own
    /// harvest works the site nearest the town the copy <paramref name="uniqueId"/> homes in.
    /// </summary>
    public static void Apply(CharacterDefinition composed, string kind, string uniqueId)
    {
        if (composed == null || !TradesByKind.ContainsKey(kind ?? string.Empty))
        {
            return;
        }

        var routines = new Dictionary<string, List<SkillStepDefinition>>(StringComparer.OrdinalIgnoreCase);
        var dropped = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CraftRoutineId };

        foreach (var (id, steps) in composed.ResolvedRoutines())
        {
            if (Gathers(steps))
            {
                dropped.Add(id);
            }
            else
            {
                routines[id] = steps;
            }
        }

        routines[CraftRoutineId] = CraftRoutine(kind, WorkSites.HomeFor(composed.Spawn, uniqueId, kind));
        var choices = new List<ChoiceDefinition>();

        foreach (var choice in composed.Choices ?? [])
        {
            if (choice?.Routine != null && !dropped.Contains(choice.Routine))
            {
                choices.Add(choice);
            }
        }

        choices.Add(new ChoiceDefinition { Routine = CraftRoutineId, Weight = CraftChoiceWeight, Description = DescriptionOf(kind) });
        composed.Routines = routines;
        composed.Choices = choices;
    }

    /// <summary>
    /// The steps of a crafter's day: work the station, sell, restock, bank, and hawk what it kept.
    /// A trade with a harvest of its own (<see cref="CraftTrade.GatherKind"/>) also works the
    /// site of that harvest nearest <paramref name="home"/> and walks the load back to town, so a
    /// smith with no ingots for sale digs and smelts its own.
    /// </summary>
    public static List<SkillStepDefinition> CraftRoutine(string kind, Point3D home)
    {
        List<SkillStepDefinition> steps = [new() { Skill = kind }];

        if (TradeByKind(kind)?.GatherKind is { } gather)
        {
            var site = WorkSites.NearestSite(gather, home).Harvest;
            steps.Add(new() { Skill = gather, Area = new AreaDefinition { X = site.X, Y = site.Y, Width = site.Width, Height = site.Height } });
            steps.Add(new() { Skill = SkillKinds.GoTo, Target = CharactersFile.DefaultBankSpot });
        }

        steps.AddRange(
        [
            new() { Skill = SkillKinds.VendorSell },
            new() { Skill = SkillKinds.VendorBuy },
            new() { Skill = SkillKinds.BankDeposit, BankSpot = CharactersFile.DefaultBankSpot },
            new() { Skill = SkillKinds.BankShop, BankSpot = CharactersFile.DefaultBankSpot },
            new() { Skill = SkillKinds.Decide }
        ]);
        return steps;
    }

    /// <summary>
    /// The station trade a person of <paramref name="personClass"/> lives by, when its routines
    /// run that trade; null for a gatherer, a fighter or anyone else.
    /// </summary>
    public static string TradeOf(PersonClass personClass, Func<string, bool> usesSkill)
    {
        string kind = null;

        for (var i = 0; i < TradeClasses.Length && kind == null; i++)
        {
            if (TradeClasses[i].Class == personClass)
            {
                kind = TradeClasses[i].Kind;
            }
        }

        return kind != null && usesSkill?.Invoke(kind) == true ? kind : null;
    }

    /// <summary>The trade of a craft career kind, or null for any other skill.</summary>
    public static CraftTrade TradeByKind(string kind) =>
        kind != null && TradesByKind.TryGetValue(kind, out var trade) ? trade : null;

    /// <summary>True when <paramref name="kind"/> is a trade a crafter can live by.</summary>
    public static bool IsCareer(string kind) => TradeByKind(kind) != null;

    /// <summary>The towns a crafter of <paramref name="kind"/> homes in, or empty for any other skill.</summary>
    public static IReadOnlyList<WorkSite> HomesFor(string kind) =>
        kind != null && HomeTowns.TryGetValue(kind, out var towns) ? towns : [];

    public static string DescriptionOf(string kind)
    {
        for (var i = 0; i < Careers.Length; i++)
        {
            if (string.Equals(Careers[i].Kind, kind, StringComparison.OrdinalIgnoreCase))
            {
                return Careers[i].Description;
            }
        }

        return null;
    }

    private static bool Gathers(List<SkillStepDefinition> steps)
    {
        for (var i = 0; i < (steps?.Count ?? 0); i++)
        {
            if (steps[i]?.Skill is SkillKinds.Mine or SkillKinds.Lumberjack or SkillKinds.Fish or SkillKinds.Patrol
                or SkillKinds.Boat)
            {
                return true;
            }
        }

        return false;
    }

    private static WorkSite[] Towns(params (Point3D Home, string Name)[] towns)
    {
        var sites = new WorkSite[towns.Length];

        for (var i = 0; i < towns.Length; i++)
        {
            sites[i] = new WorkSite(towns[i].Name, towns[i].Home, new Rectangle2D(towns[i].Home.X, towns[i].Home.Y, 1, 1));
        }

        return sites;
    }
}
