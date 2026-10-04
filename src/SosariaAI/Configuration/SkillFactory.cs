using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Configuration;

public static class SkillFactory
{
    public const int DefaultHuntMinutes = 15;
    public const int DefaultDungeonMinutes = 20;
    public const int DefaultRestMinutes = 2;
    public const string DefaultTavernDestination = ShopFinder.TavernToken;
    public const string DefaultSightseeDestination = "shrine";

    /// <summary>
    /// The skill a step runs. A practice skill is one skill check; it runs as a practice
    /// session, so the person works the skill for a few minutes instead of one tick.
    /// </summary>
    public static Skill Create(SkillStepDefinition step, FacetContent facet, string facetName)
    {
        var skill = CreateSingle(step, facet, facetName);
        return PracticeRules.IsPractice(step.Skill) ? new PracticeSession(skill) : skill;
    }

    private static Skill CreateSingle(SkillStepDefinition step, FacetContent facet, string facetName)
    {
        if (step == null || string.IsNullOrWhiteSpace(step.Skill))
        {
            throw new FormatException("A routine step must name a skill.");
        }

        if (PracticeSkillTable.TryGet(step.Skill, out var practice))
        {
            return new PracticeSkill(practice);
        }

        var mapName = string.IsNullOrWhiteSpace(facetName) ? CharactersFile.DefaultMapName : facetName;

        return step.Skill switch
        {
            SkillKinds.Travel => step.Target == Point3D.Zero
                ? throw new FormatException("A Travel step needs a target.")
                : new TownTrip(step.Target),
            SkillKinds.Arrive => new ArrivalStay(),
            SkillKinds.Browse => new BrowseSkill(),
            SkillKinds.GoTo => CreateTravel(step),
            SkillKinds.Lumberjack => CreateLumberjack(step, facet),
            SkillKinds.Mine => CreateHarvest(step, facet, SkillKinds.Mine),
            SkillKinds.Fish => CreateHarvest(step, facet, SkillKinds.Fish),
            SkillKinds.BankDeposit => new BankDepositSkill(
                step.BankSpot == Point3D.Zero ? CharactersFile.DefaultBankSpot : step.BankSpot
            ),
            SkillKinds.BankShop => new BankShopSkill(
                step.BankSpot == Point3D.Zero ? CharactersFile.DefaultBankSpot : step.BankSpot
            ),
            SkillKinds.VendorBuy => new VendorBuySkill(),
            SkillKinds.VendorSell => new VendorSellSkill(
                string.IsNullOrWhiteSpace(step.Destination) ? null : step.Destination
            ),
            SkillKinds.IdleWander => new IdleWanderSkill(
                step.Center,
                step.Radius ?? CharactersFile.DefaultIdleRadius,
                step.Duration ?? CharactersFile.DefaultIdleDuration
            ),
            SkillKinds.Patrol => CreatePatrol(step),
            SkillKinds.Hunt => CreateHunt(step, facet, mapName),
            SkillKinds.Dungeon => CreateDungeon(step, mapName),
            SkillKinds.Follow => new FollowSkill(
                FacetIds.Prefix(mapName, step.Party ?? step.LeaderId),
                step.Range ?? SosariaCombat.FollowRangeMin
            ),
            SkillKinds.Rest => new RestSkill(
                step.Duration ?? TimeSpan.FromMinutes(step.Minutes ?? DefaultRestMinutes)
            ),
            SkillKinds.Decide => new DecideSkill(),
            SkillKinds.UpgradeGear => new UpgradeGearSkill(),
            SkillKinds.Steal => new StealSkill(),
            SkillKinds.Hide => new HideSkill(),
            SkillKinds.DetectHidden => new DetectHiddenSkill(),
            SkillKinds.Snoop => new SnoopSkill(),
            SkillKinds.Lockpick => new LockpickSkill(),
            SkillKinds.Tame => new TameSkill(),
            SkillKinds.Mount => new MountSkill(),
            SkillKinds.BuyMount => new BuyMountSkill(),
            SkillKinds.Smith => new SmithSkill(),
            SkillKinds.PlayerVendor => new PlayerVendorSkill(),
            SkillKinds.Boat => new BoatSkill(),
            SkillKinds.Mark => new MarkSkill(),
            SkillKinds.Heal => new HealSkill(),
            SkillKinds.Alchemy => new AlchemySkill(),
            SkillKinds.Inscription => new InscriptionSkill(),
            SkillKinds.Peace => new PeaceSkill(),
            SkillKinds.Track => new TrackSkill(),
            SkillKinds.Tailor => new TailorSkill(),
            SkillKinds.Poison => new PoisonSkill(),
            SkillKinds.Carpentry => new CarpentrySkill(),
            SkillKinds.Cook => new CookSkill(),
            SkillKinds.Fletch => new FletchSkill(),
            SkillKinds.Tinker => new TinkerSkill(),
            SkillKinds.Meditate => new MeditateSkill(),
            SkillKinds.Cartography => new CartographySkill(),
            SkillKinds.Lore => new LoreSkill(),
            SkillKinds.Vet => new VetSkill(),
            SkillKinds.Spirit => new SpiritSkill(),
            SkillKinds.Discord => new DiscordSkill(),
            SkillKinds.Provoke => new ProvokeSkill(),
            SkillKinds.Beg => new BegSkill(),
            SkillKinds.RemoveTrap => new RemoveTrapSkill(),
            SkillKinds.Stealth => new StealthSkill(),
            SkillKinds.Mage => new MageSkill(),
            SkillKinds.Resist => new ResistSkill(),
            SkillKinds.Gate => new GateSkill(),
            SkillKinds.Recall => new RecallSkill(),
            SkillKinds.BankCrowd => new BankCrowdSkill(),
            SkillKinds.Tavern => new TavernSkill(
                string.IsNullOrWhiteSpace(step.Destination) ? DefaultTavernDestination : step.Destination
            ),
            SkillKinds.Visit => new VisitSkill(
                string.IsNullOrWhiteSpace(step.Destination) ? DefaultTavernDestination : step.Destination
            ),
            SkillKinds.Sightsee => new SightseeSkill(
                string.IsNullOrWhiteSpace(step.Destination)
                    ? DefaultSightseeDestination
                    : step.Destination
            ),
            SkillKinds.Loiter => new LoiterSkill(
                step.Center,
                step.Radius ?? CharactersFile.DefaultIdleRadius,
                step.Duration ?? CharactersFile.DefaultIdleDuration
            ),
            SkillKinds.House => new HouseSkill(),
            SkillKinds.Flee => new FleeSkill(),
            SkillKinds.GoHome => new GoHomeSkill(),
            SkillKinds.Conflict => new ConflictSkill(),
            _ => throw new FormatException($"Unknown skill '{step.Skill}'.")
        };
    }

    private static Skill CreateTravel(SkillStepDefinition step)
    {
        var range = step.Range ?? CharactersFile.DefaultGoToRange;

        if (!string.IsNullOrWhiteSpace(step.Destination))
        {
            return new TravelSkill(step.Destination, range);
        }

        if (step.Target != Point3D.Zero)
        {
            return new TravelSkill(step.Target, range);
        }

        if (!string.IsNullOrWhiteSpace(step.Dungeon))
        {
            return new TravelSkill(step.Dungeon, range);
        }

        throw new FormatException("A GoTo step needs a target or a destination.");
    }

    private static LumberjackSkill CreateLumberjack(SkillStepDefinition step, FacetContent facet) =>
        new(RequireArea(step, facet, SkillKinds.Lumberjack), step.FillFraction ?? CharactersFile.DefaultFillFraction);

    private static Skill CreateHarvest(SkillStepDefinition step, FacetContent facet, string kind)
    {
        var area = RequireArea(step, facet, kind);
        var fill = step.FillFraction ?? CharactersFile.DefaultFillFraction;
        return kind == SkillKinds.Mine ? new MineSkill(area, fill) : new FishSkill(area, fill);
    }

    private static HuntSkill CreateHunt(SkillStepDefinition step, FacetContent facet, string facetName)
    {
        var area = RequireArea(step, facet, SkillKinds.Hunt);
        var duration = TimeSpan.FromMinutes(step.Minutes ?? DefaultHuntMinutes);
        var stopBelow = step.StopBelowHitsFraction ?? SosariaCombat.DefaultStopBelowHitsFraction;
        var range = step.Range ?? CharactersFile.DefaultGoToRange;
        var dest = step.Target != Point3D.Zero
            ? step.Target
            : new Point3D(area.X + Math.Max(1, area.Width) / 2, area.Y + Math.Max(1, area.Height) / 2, 0);
        var approach = new TravelSkill(dest, range);

        return new HuntSkill(area, duration, stopBelow, approach, FacetIds.Prefix(facetName, step.Party));
    }

    /// <summary>A trip to a catalog dungeon hall: the graph walks it in, then it hunts the hall's ground.</summary>
    private static DungeonTripSkill CreateDungeon(SkillStepDefinition step, string facetName)
    {
        if (step.Target == Point3D.Zero)
        {
            throw new FormatException("A Dungeon step needs a target.");
        }

        var partyId = FacetIds.Prefix(facetName, step.Party);
        var hunt = new HuntSkill(
            step.Area?.ToRectangle() ?? HuntGround.AreaAround(step.Target).ToRectangle(),
            TimeSpan.FromMinutes(step.Minutes ?? DefaultDungeonMinutes),
            step.StopBelowHitsFraction ?? SosariaCombat.DefaultStopBelowHitsFraction,
            approach: null,
            partyId
        );
        return new DungeonTripSkill(step.Target, hunt, partyId);
    }

    /// <summary>A group's run to a catalog dungeon hall, fought round the hall. No authored crew.</summary>
    public static DungeonTripSkill DungeonTrip(Point3D hall) => new(hall, HallHunt(hall, partyId: null), partyId: null);

    /// <summary>The hunt a run holds round a catalog hall: the hall's ground, the default run time and stop.</summary>
    public static HuntSkill HallHunt(Point3D hall, string partyId) =>
        new(
            HuntGround.AreaAround(hall).ToRectangle(),
            TimeSpan.FromMinutes(DefaultDungeonMinutes),
            SosariaCombat.DefaultStopBelowHitsFraction,
            approach: null,
            partyId
        );

    /// <summary>A group's hunt at a catalog ground, walked to by the graph. No authored crew.</summary>
    public static HuntSkill GroundHunt(Point3D ground) =>
        new(
            HuntGround.AreaAround(ground).ToRectangle(),
            TimeSpan.FromMinutes(DefaultHuntMinutes),
            SosariaCombat.DefaultStopBelowHitsFraction,
            new TravelSkill(ground, CharactersFile.DefaultGoToRange),
            partyId: null
        );

    private static Rectangle2D RequireArea(SkillStepDefinition step, FacetContent facet, string kind)
    {
        if (step.Area == null)
        {
            throw new FormatException($"A {kind} step needs an area.");
        }

        if (step.Area.IsNamed)
        {
            if (facet == null)
            {
                throw new FormatException($"Unknown area '{step.Area.Name}'.");
            }

            return facet.ResolveArea(step.Area);
        }

        return step.Area.ToRectangle();
    }

    private static PatrolSkill CreatePatrol(SkillStepDefinition step)
    {
        if (step.Points == null || step.Points.Count == 0)
        {
            throw new FormatException("A Patrol step needs at least one point.");
        }

        return new PatrolSkill(step.Points);
    }
}
