using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Regions;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>A place a tamer goes to tame: a catalog spawn spot and the kind of beast it holds.</summary>
public sealed record TamingGround(Destination Place, BeastProfile Beast);

/// <summary>
/// Where the tamable beasts of this world live, and which of them a tamer goes for. The nav
/// catalog lists every spawner spot with its creature as the role (the dragons and drakes of
/// Destard, the nightmares of Terathan Keep, the white wyrm of Ice); what a kind asks of a
/// tamer is read off a live wild one, so only the beasts this era's world spawns are known,
/// on the engine's own numbers. A tamer goes for the strongest beast its skill and slots take
/// (<see cref="TameRules.Choose"/>): a keeper for its tier, or a practice beast while it
/// practises (<see cref="TameRules.IsQuarry"/>). It goes on ground it walks to or has a rune
/// for, never inside a town, never ground it found empty lately, and never in an area where
/// <see cref="MaxTamersPerArea"/> tamers already work: the area is a whole dungeon, whose many
/// spawn spots once drew a dozen tamers to Destard at once, or open ground within
/// <see cref="TamingAreaTiles"/>.
/// </summary>
public static class TamingGrounds
{
    /// <summary>The live beasts are looked over again this often: a spawner fills and empties.</summary>
    public static readonly TimeSpan SampleRefresh = TimeSpan.FromMinutes(15);

    /// <summary>A ground a tamer found no beast on stays closed to it this long.</summary>
    public static readonly TimeSpan DryFor = TimeSpan.FromMinutes(30);

    /// <summary>A tamer's pick holds this long while its skill and followers stay the same.</summary>
    public static readonly TimeSpan PickKeep = TimeSpan.FromMinutes(5);

    /// <summary>An area takes this many tamers at a time; the next one goes elsewhere.</summary>
    public const int MaxTamersPerArea = 2;

    /// <summary>Two grounds on open land this close share one area.</summary>
    public const int TamingAreaTiles = 64;

    /// <summary>A pick tests this many grounds, best first, for a way there before it gives up.</summary>
    public const int MaxReachLooks = 8;

    /// <summary>A walk ends at a road node this close to the ground, or the ground is off the roads.</summary>
    public const int NodeReachTiles = HuntGround.AreaRadius;

    private static readonly Dictionary<string, BaseCreature> Samples = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<(Serial Tamer, Point3D Ground), DateTime> DryUntil = new();
    private static readonly Dictionary<Serial, (Point3D Ground, Map Map, string Dungeon)> Claims = new();
    private static readonly Dictionary<Serial, (TamingGround Ground, DateTime At, int Followers, int Taming, bool Practises)> Picks = new();
    private static DateTime _sampledAt;

    /// <summary>What the engine says about this beast's kind.</summary>
    public static BeastProfile ProfileOf(BaseCreature creature) =>
        new(
            creature.GetType().Name,
            creature.MinTameSkill,
            creature.ControlSlots,
            TameRules.BeastPower(creature.HitsMax, creature.Str, creature.DamageMin, creature.DamageMax),
            creature.AllowFemaleTamer,
            creature.AllowMaleTamer,
            creature.SubdueBeforeTame
        );

    /// <summary>
    /// The tamer as the engine's checks read it, whether it practises now (<see cref="Practises"/>),
    /// and the fighting pet it keeps (<see cref="PetKeeper.FighterPower"/>). With
    /// <paramref name="freedSlots"/>, its followers as they would stand with pets of those slots
    /// in the stables.
    /// </summary>
    public static TamerFacts FactsOf(Mobile tamer, int freedSlots = 0) =>
        new(
            tamer.Skills.AnimalTaming.Value,
            tamer.Female,
            Math.Max(0, tamer.Followers - freedSlots),
            tamer.FollowersMax,
            Practises(tamer),
            PetKeeper.FighterPower(tamer)
        );

    /// <summary>
    /// The tamer's taming still gains by the engine's rules (<see cref="TameRules.StillGains"/>)
    /// and its practice session is open (<see cref="TamePractice"/>).
    /// </summary>
    public static bool Practises(Mobile tamer)
    {
        var taming = tamer.Skills.AnimalTaming;
        var otherFalls = false;

        for (var i = 0; i < tamer.Skills.Length && !otherFalls; i++)
        {
            var other = tamer.Skills[i];
            otherFalls = other != taming && other.Lock == SkillLock.Down && other.BaseFixedPoint > 0;
        }

        return TameRules.StillGains(
                   taming.Base,
                   taming.Cap,
                   taming.Lock == SkillLock.Up,
                   tamer.Skills.Total,
                   tamer.Skills.Cap,
                   otherFalls
               ) &&
               TamePractice.IsOpen(tamer, Core.Now);
    }

    /// <summary>True inside a town or under the guards: no taming ground, and no place to set a pet on anyone.</summary>
    public static bool InTown(Point3D at, Map map) =>
        map != null && map != Map.Internal &&
        (GuardCall.IsGuardedPlace(at, map) || Region.Find(at, map).IsPartOf<TownRegion>());

    /// <summary>A live wild beast out of town that this tamer may try now (<see cref="TameRules.IsLiveQuarry"/>).</summary>
    public static bool IsQuarry(Mobile tamer, BaseCreature creature) => tamer != null && IsQuarry(tamer, creature, FactsOf(tamer));

    /// <summary>A live wild beast out of town that a tamer of these facts may try.</summary>
    private static bool IsQuarry(Mobile tamer, BaseCreature creature, TamerFacts facts) =>
        creature is { Deleted: false, Alive: true, IsDeadPet: false, Summoned: false } &&
        !InTown(creature.Location, creature.Map) &&
        TameRules.IsLiveQuarry(
            creature.Tamable,
            creature.Controlled,
            creature.Owners.Contains(tamer),
            ProfileOf(creature),
            facts,
            creature.Owners.Count,
            creature.GetControlChance(tamer)
        );

    /// <summary>
    /// The ground this tamer goes to next, or null when no beast suits it: a ground where a live
    /// beast of its kind it may try stands now (<see cref="HoldsQuarry"/>).
    /// </summary>
    public static TamingGround Pick(SosariaCharacter tamer)
    {
        if (!People.InWorld(tamer))
        {
            return null;
        }

        var taming = (int)tamer.Skills.AnimalTaming.Value;
        var practises = Practises(tamer);

        if (Picks.TryGetValue(tamer.Serial, out var kept) && Core.Now - kept.At < PickKeep &&
            kept.Followers == tamer.Followers && kept.Taming == taming && kept.Practises == practises && kept.Ground != null &&
            !IsDry(tamer, kept.Ground.Place.Arrival) && !IsCrowded(tamer, kept.Ground.Place.Arrival) &&
            HoldsQuarry(tamer, tamer.Map, kept.Ground, FactsOf(tamer)))
        {
            return kept.Ground;
        }

        var ranked = Ranked(tamer, tamer.Map, tamer.HomeSpot, 1, liveOnly: true);
        var pick = ranked.Count == 0 ? null : ranked[0];
        Picks[tamer.Serial] = (pick, Core.Now, tamer.Followers, taming, practises);
        return pick;
    }

    /// <summary>
    /// Up to <paramref name="max"/> grounds for this tamer, best first, each one it can reach
    /// from <paramref name="from"/> on <paramref name="map"/>: on foot, or by a rune it carries.
    /// With <paramref name="freedSlots"/>, the grounds its slots would hold with pets of those
    /// slots in the stables. With <paramref name="liveOnly"/>, only grounds that hold a beast now
    /// (<see cref="HoldsQuarry"/>), for a trip; a rune for a ground is marked without one.
    /// </summary>
    public static List<TamingGround> Ranked(
        SosariaCharacter tamer,
        Map map,
        Point3D from,
        int max,
        int freedSlots = 0,
        bool liveOnly = false
    )
    {
        var picks = new List<TamingGround>();
        var catalog = NavWorld.DestinationsFor(tamer?.HomeFacet);

        if (catalog == null || map == null || map == Map.Internal || max <= 0)
        {
            return picks;
        }

        EnsureSamples();

        var facts = FactsOf(tamer, freedSlots);
        var grounds = new List<TamingGround>();
        var options = new List<(int Power, int Distance)>();

        for (var i = 0; i < catalog.All.Count; i++)
        {
            var place = catalog.All[i];

            if (place.ParsedKind != DestinationKind.Hunt || string.IsNullOrWhiteSpace(place.Role) ||
                !Samples.TryGetValue(place.Role, out var sample) || sample.Deleted)
            {
                continue;
            }

            var beast = ProfileOf(sample);

            if (!TameRules.IsQuarry(beast, facts, owners: 0, sample.GetControlChance(tamer)) ||
                InTown(place.Arrival, map) || IsDry(tamer, place.Arrival) || IsCrowded(tamer, place.Arrival))
            {
                continue;
            }

            grounds.Add(new TamingGround(place, beast));
            options.Add((beast.Power, NavMetric.Chebyshev(from, place.Arrival)));
        }

        for (var looks = 0; looks < MaxReachLooks && picks.Count < max; looks++)
        {
            var index = TameRules.Choose(options);

            if (index == TameRules.NoChoice)
            {
                break;
            }

            var ground = grounds[index];
            grounds.RemoveAt(index);
            options.RemoveAt(index);

            if (!picks.Exists(kept => kept.Place.Arrival == ground.Place.Arrival) &&
                (!liveOnly || HoldsQuarry(tamer, map, ground, facts)) && Reaches(tamer, map, from, ground.Place.Arrival))
            {
                picks.Add(ground);
            }
        }

        return picks;
    }

    /// <summary>
    /// True when a live wild beast of the ground's kind that a tamer of these facts may try
    /// stands within a tamer's sight of the ground's spot (<see cref="TameSkill.GroundSightTiles"/>).
    /// The catalog lists every spot of a spawner under each kind it spawns, and a kind known alive
    /// anywhere put every such spot on the list: the Dagger Isle spawners mix four kinds over 80
    /// tiles and refill in 20 minutes, and 62 trips found no beast at the spot.
    /// </summary>
    private static bool HoldsQuarry(Mobile tamer, Map map, TamingGround ground, TamerFacts facts)
    {
        foreach (var mobile in map.GetMobilesInRange(ground.Place.Arrival, TameSkill.GroundSightTiles))
        {
            if (mobile is BaseCreature creature && string.Equals(ground.Beast.Kind, creature.GetType().Name, StringComparison.Ordinal) &&
                IsQuarry(tamer, creature, facts))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The tamer works this ground now; others count it against <see cref="MaxTamersPerArea"/>.</summary>
    public static void Claim(SosariaCharacter tamer, Point3D ground) =>
        Claims[tamer.Serial] = (ground, tamer.Map, DungeonOf(ground, tamer.Map));

    /// <summary>The tamer works no ground any more.</summary>
    public static void Unclaim(SosariaCharacter tamer)
    {
        if (tamer != null)
        {
            Claims.Remove(tamer.Serial);
        }
    }

    /// <summary>
    /// True when <see cref="MaxTamersPerArea"/> other tamers work the area of this ground: a
    /// tamer leaves the practice to them and goes elsewhere.
    /// </summary>
    public static bool IsCrowded(SosariaCharacter tamer, Point3D ground)
    {
        var map = tamer.Map;
        var dungeon = DungeonOf(ground, map);
        var gone = new List<Serial>();
        var others = 0;

        foreach (var (serial, claim) in Claims)
        {
            if (World.FindMobile(serial) is not SosariaCharacter { Deleted: false })
            {
                gone.Add(serial);
            }
            else if (serial != tamer.Serial && claim.Map == map && SameArea(ground, dungeon, claim.Ground, claim.Dungeon))
            {
                others++;
            }
        }

        for (var i = 0; i < gone.Count; i++)
        {
            Claims.Remove(gone[i]);
        }

        return others >= MaxTamersPerArea;
    }

    /// <summary>
    /// Two grounds share an area: both in the same dungeon, or both on open ground within
    /// <see cref="TamingAreaTiles"/>. A dungeon name is null for open ground.
    /// </summary>
    public static bool SameArea(Point3D ground, string dungeon, Point3D other, string otherDungeon) =>
        dungeon != null || otherDungeon != null
            ? string.Equals(dungeon, otherDungeon, StringComparison.Ordinal)
            : NavMetric.Chebyshev(ground, other) <= TamingAreaTiles;

    private static string DungeonOf(Point3D ground, Map map) =>
        map == null || map == Map.Internal ? null : Region.Find(ground, map).GetRegion<DungeonRegion>()?.Name;

    /// <summary>The tamer found no beast here: it tries other ground for a while.</summary>
    public static void MarkDry(SosariaCharacter tamer, Point3D ground)
    {
        DryUntil[(tamer.Serial, ground)] = Core.Now + DryFor;
        Picks.Remove(tamer.Serial);
    }

    private static bool IsDry(SosariaCharacter tamer, Point3D ground) =>
        DryUntil.TryGetValue((tamer.Serial, ground), out var until) && Core.Now < until;

    /// <summary>A rune the tamer can recall on lands near the ground, or its roads walk there.</summary>
    private static bool Reaches(SosariaCharacter tamer, Map map, Point3D from, Point3D ground)
    {
        if (tamer.Skills.Magery.Value >= RecallRules.MinMagery && RuneShelf.MarkedNear(tamer, ground, map) != null)
        {
            return true;
        }

        var node = NavWorld.GraphFor(map.Name)?.FindNearest(ground);
        return node != null && NavMetric.Chebyshev(node.Location, ground) <= NodeReachTiles &&
               RedGangReach.Walks(tamer, map, from, ground);
    }

    /// <summary>One live wild beast of each tamable kind in the world, looked over again every <see cref="SampleRefresh"/>.</summary>
    private static void EnsureSamples()
    {
        if (Samples.Count > 0 && Core.Now - _sampledAt < SampleRefresh)
        {
            return;
        }

        Samples.Clear();
        _sampledAt = Core.Now;

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is BaseCreature { Deleted: false, Tamable: true, Controlled: false, IsDeadPet: false, Summoned: false } creature &&
                People.InWorld(creature))
            {
                Samples.TryAdd(creature.GetType().Name, creature);
            }
        }
    }
}
