using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using Server.Guilds;
using Server.Logging;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Population;
using SosariaAI.Social;
using SosariaAI.Logging;
using SosariaAI.Navigation;

namespace SosariaAI.Spawning;

/// <summary>
/// Binds saved characters to the per-facet spawn plan by unique id, then spawns any
/// plan entry that has no mobile.
/// </summary>
public static class CharacterSpawner
{
    private static readonly ILogger logger = SosariaLog.For(typeof(CharacterSpawner));
    private static readonly ILogger console = SosariaLog.Console(typeof(CharacterSpawner));

    private const int SpawnSearchRange = 16;

    /// <summary>The CallPriority the engine gives an Initialize that names none (CallPriorityComparer).</summary>
    private const int EngineDefaultCallPriority = 50;

    /// <summary>
    /// The engine runs every Initialize in CallPriority order with a sort that is not stable,
    /// so a tie with <see cref="SosariaSettings.Initialize"/> could bring the people in before
    /// the moongate guards were off and the road graph and the hot spots were read.
    /// </summary>
    private const int InitializePriority = EngineDefaultCallPriority + 1;

    /// <summary>People bound or spawned per loop turn. A big plan spreads over turns so boot does not freeze the world.</summary>
    private const int SpawnSliceSize = 40;

    /// <summary>Pause between slices so timers and network get real turns during the wake-up.</summary>
    private static readonly TimeSpan SpawnSliceGap = TimeSpan.FromMilliseconds(25);

    /// <summary>Full route proofs a single spawn resolve may run after the anchor's own proof.</summary>
    private const int FallbackProofs = 8;

    private static readonly Dictionary<Point3D, (int Count, SpawnReject Why)> CrowdedSites = new();
    private static readonly Dictionary<SpawnReject, int> Unplaced = new();
    private static bool _populationBooted;

    /// <summary>Each facet plan's reds and their gangs, chosen once per boot.</summary>
    private static readonly ConditionalWeakTable<FacetSpawnRequest, List<(string Id, int Gang)>> RedRosters = new();

    private sealed class SpawnWork
    {
        public FacetSpawnRequest Request;
        public SpawnPlanEntry Entry;
        public CharacterDefinition Definition;
        public SosariaCharacter Existing;
        public Map Map;
        public int Crowd;
    }

    [CallPriority(InitializePriority)]
    public static void Initialize()
    {
        var assemblyName = typeof(CharacterSpawner).Assembly.GetName();
        console.Information("{Assembly} {Version} loaded", assemblyName.Name, assemblyName.Version);
        console.Information("SosariaAI era is {Expansion}", EraRules.Current());

        // A fresh save has no bankers, vendors or gates. The people wait for the staff's
        // First Time Setup instead of walking into a bare world.
        if (WorldSetup.AwaitsSetup())
        {
            console.Warning(WorldSetup.WaitingLine);
            return;
        }

        BootPopulation();
    }

    /// <summary>Brings the population in once First Time Setup has built the world.</summary>
    public static void StartAfterSetup()
    {
        if (!_populationBooted)
        {
            BootPopulation();
        }
    }

    private static void BootPopulation()
    {
        _populationBooted = true;
        var warnings = new List<FacetPlanWarning>();
        var requests = FacetSpawnPlanner.Plan(SosariaSettings.Characters, warnings);

        for (var i = 0; i < warnings.Count; i++)
        {
            logger.Warning(warnings[i].Template, warnings[i].Facet, warnings[i].Count, warnings[i].Needed);
        }

        var parties = new List<PartyDefinition>();

        for (var i = 0; i < requests.Count; i++)
        {
            CollectParties(requests[i], parties);
            ReserveFixtureNames(requests[i]);
        }

        Party.Configure(parties);

        // A rebuilt graph can change which anchor tiles reach the world.
        HomeSpotRules.ResetRoutableCache();

        var existing = FindExistingCharacters();
        var claimed = new HashSet<SosariaCharacter>();
        var leftover = 0;
        var work = new List<SpawnWork>();

        for (var i = 0; i < requests.Count; i++)
        {
            BindFacet(requests[i], existing, claimed, work);
        }

        for (var i = 0; i < existing.Count; i++)
        {
            var character = existing[i];

            if (claimed.Contains(character))
            {
                continue;
            }

            // A saved person the plan no longer counts is gone: everyone the server keeps is in the world.
            leftover++;
            character.Delete();
        }

        if (leftover > 0)
        {
            console.Information(
                "{Count} saved people were over the plan cap in characters.json and were removed",
                leftover
            );
        }

        // A save that lands mid-pump keeps the people already bound; the rest spawn
        // fresh next boot, so a partial drain loses nothing.
        if (work.Count == 0)
        {
            FinishBoot();
            return;
        }

        PumpSlice(work, 0);
    }

    private static void PumpSlice(List<SpawnWork> work, int index)
    {
        var end = Math.Min(index + SpawnSliceSize, work.Count);

        for (; index < end; index++)
        {
            var item = work[index];
            SpawnSpread.PlannedCount = item.Crowd;

            if (item.Existing != null)
            {
                Bind(item.Existing, item.Definition, item.Request, item.Entry, alreadyInWorld: true, item.Map);
            }
            else
            {
                Spawn(item.Definition, item.Request, item.Entry, item.Map);
            }
        }

        if (index < work.Count)
        {
            Timer.DelayCall(SpawnSliceGap, PumpSlice, work, index);
            return;
        }

        FinishBoot();
        console.Information("Population bind finished: {Count} people placed", work.Count);
    }

    private static void FinishBoot()
    {
        ReportCrowdedSites();
        LifecycleClock.AfterWorldLoad();
        MemoryStore.Shared.Flush();
    }

    /// <summary>
    /// Where a person logs in: its scattered spot, else its site, else a tile by the bank
    /// nearest its site, as a player whose house spot was taken logs in at the bank. A spot
    /// inside a dungeon is never used (<see cref="SpawnPlacementRules.OutsideDungeons"/>). False
    /// only when even the bank has no free tile; <paramref name="rejects"/> then counts the
    /// tests that turned the tiles down.
    /// </summary>
    private static bool TryPlace(
        Map map,
        string facet,
        Point3D configured,
        Point3D home,
        string uniqueId,
        Dictionary<SpawnReject, int> rejects,
        out Point3D location,
        out bool atBank
    )
    {
        atBank = false;
        var dungeonAt = DungeonGround.PlacesToLeave(map);

        // A dock or shore site scattered wide lands a copy in the sea, and a 16-tile
        // search round that spot finds only more sea. Fall back to the site itself.
        if (TryResolveSpawnLocation(map, configured, home, rejects, out location) && OutsideDungeons(location, dungeonAt, rejects) ||
            TryResolveSpawnLocation(map, home, home, rejects, out location) && OutsideDungeons(location, dungeonAt, rejects))
        {
            return true;
        }

        atBank = true;
        var bank = NavWorld.DestinationsFor(facet)?.Nearest(home, DestinationKind.Bank);
        return bank != null && HomeSpotRules.TryBankTile(map, bank.Arrival, uniqueId, rejects, out location) &&
               OutsideDungeons(location, dungeonAt, rejects);
    }

    /// <summary>True when the spot lies in no dungeon; a spot inside one is counted in <paramref name="rejects"/>.</summary>
    private static bool OutsideDungeons(Point3D at, Func<Point3D, string> dungeonAt, Dictionary<SpawnReject, int> rejects)
    {
        if (SpawnPlacementRules.OutsideDungeons(at, dungeonAt))
        {
            return true;
        }

        HomeSpotRules.Count(rejects, SpawnReject.InDungeon);
        return false;
    }

    // One line per crowded site instead of one error per person, naming the test that turned the site down.
    private static void ReportCrowdedSites()
    {
        foreach (var (site, (count, why)) in CrowdedSites)
        {
            console.Warning(
                "{Count} people had no room at {Site} ({Why}); they started at the nearest bank",
                count,
                site,
                SpawnPlacementRules.Describe(why)
            );
        }

        var unplaced = 0;

        foreach (var count in Unplaced.Values)
        {
            unplaced += count;
        }

        if (unplaced > 0)
        {
            console.Warning(
                "{Count} people had no room at their site or its bank and were not spawned: {Why}",
                unplaced,
                SpawnPlacementRules.Summary(Unplaced)
            );
        }

        CrowdedSites.Clear();
        Unplaced.Clear();
    }

    private static void CollectParties(FacetSpawnRequest request, List<PartyDefinition> parties)
    {
        var source = request.Content?.Parties;

        if (source == null)
        {
            return;
        }

        for (var i = 0; i < source.Count; i++)
        {
            var qualified = FacetIds.QualifyParty(request.Facet, source[i]);

            if (qualified != null)
            {
                parties.Add(qualified);
            }
        }
    }

    /// <summary>A copy binding before a fixture spawns must not take the fixture's name.</summary>
    private static void ReserveFixtureNames(FacetSpawnRequest request)
    {
        var roster = request.Content?.Roster;

        if (roster == null)
        {
            return;
        }

        for (var i = 0; i < roster.Count; i++)
        {
            if (roster[i] != null)
            {
                NameRegistry.Reserve(CharacterLooks.FixtureName(roster[i].Name, roster[i].Id));
            }
        }
    }

    private static void BindFacet(
        FacetSpawnRequest request,
        List<SosariaCharacter> existing,
        HashSet<SosariaCharacter> claimed,
        List<SpawnWork> work
    )
    {
        if (!Map.TryParse(request.Facet, null, out var map) || map == null || map == Map.Internal)
        {
            logger.Warning(FacetSpawnPlanner.UnknownFacetTemplate, request.Facet);
            return;
        }

        var remaining = new List<SpawnPlanEntry>(request.Entries);
        var crowd = request.Entries.Count;

        for (var i = 0; i < existing.Count; i++)
        {
            var character = existing[i];

            if (claimed.Contains(character) || !IsOnFacet(character, request.Facet))
            {
                continue;
            }

            var entry = CharacterMatcher.TakeMatch(character.CharacterId, request.Facet, remaining);

            if (entry == null)
            {
                continue;
            }

            var uniqueId = FacetIds.Prefix(request.Facet, entry.Value.UniqueId);
            var definition = Person(request, entry.Value.TemplateId, uniqueId);

            if (definition == null)
            {
                continue;
            }

            claimed.Add(character);

            work.Add(new SpawnWork
            {
                Request = request,
                Entry = entry.Value,
                Definition = definition,
                Existing = character,
                Map = map,
                Crowd = crowd
            });
        }

        for (var i = 0; i < remaining.Count; i++)
        {
            var entry = remaining[i];
            var definition = Person(request, entry.TemplateId, FacetIds.Prefix(request.Facet, entry.UniqueId));

            if (definition == null)
            {
                continue;
            }

            work.Add(new SpawnWork
            {
                Request = request,
                Entry = entry,
                Definition = definition,
                Map = map,
                Crowd = crowd
            });
        }
    }

    private static CharacterDefinition Person(FacetSpawnRequest request, string templateId, string uniqueId)
    {
        var slot = request.Content.FindRoster(templateId);
        return slot == null
            ? null
            : PersonMaker.Compose(uniqueId, slot, request.Content.Roster, Brain.Personas?.All, EraBands.Current());
    }

    private static bool IsOnFacet(SosariaCharacter character, string facet)
    {
        var home = character.HomeFacet;

        if (string.IsNullOrWhiteSpace(home))
        {
            home = character.HomeMapName;
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            return FacetNames.Felucca.Equals(facet, StringComparison.OrdinalIgnoreCase);
        }

        return home.Equals(facet, StringComparison.OrdinalIgnoreCase);
    }

    private static List<SosariaCharacter> FindExistingCharacters()
    {
        var found = new List<SosariaCharacter>();

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is SosariaCharacter { Deleted: false } character)
            {
                found.Add(character);
            }
        }

        return found;
    }

    private static void Spawn(
        CharacterDefinition definition,
        FacetSpawnRequest request,
        SpawnPlanEntry entry,
        Map map
    )
    {
        var uniqueId = FacetIds.Prefix(request.Facet, entry.UniqueId);

        // A red's first day starts in Buccaneer's Den: anywhere under the guards it dies.
        var home = RedGangOf(request, entry) != PkGangRules.NoGang
            ? PkRules.BucsDenHaven
            : WorkSites.HomeFor(
                definition.Spawn,
                uniqueId,
                WorkSites.PrimaryWork(definition),
                SideAtSpawn(uniqueId, definition, PersonProfileRules.Roll(uniqueId, definition))
            );
        var configured = SpawnSpread.Offset(home, uniqueId);
        var rejects = new Dictionary<SpawnReject, int>();

        // A crowded or sealed site: the person logs in at the nearest bank instead. The boot
        // summary counts them by the test that turned them down.
        if (!TryPlace(map, request.Facet, configured, home, uniqueId, rejects, out var location, out var atBank))
        {
            HomeSpotRules.Count(Unplaced, SpawnPlacementRules.Decisive(rejects));
            return;
        }

        if (atBank)
        {
            CrowdedSites[home] = (CrowdedSites.GetValueOrDefault(home).Count + 1, SpawnPlacementRules.Decisive(rejects));
        }

        var character = new SosariaCharacter();

        if (!WorkSites.IsCopy(entry.UniqueId) && !string.IsNullOrEmpty(definition.Name))
        {
            character.Name = definition.Name;
        }

        character.MoveToWorld(location, map);
        Bind(character, definition, request, entry, alreadyInWorld: false, map);
        logger.Information(
            "{Name} spawned at {Location} on {Map} as {Profile} ({Id})",
            character.Name,
            location,
            map,
            character.PersonProfile.Describe(),
            FacetIds.Prefix(request.Facet, entry.UniqueId)
        );
    }

    private static void Bind(
        SosariaCharacter character,
        CharacterDefinition definition,
        FacetSpawnRequest request,
        SpawnPlanEntry entry,
        bool alreadyInWorld,
        Map map
    )
    {
        var uniqueId = FacetIds.Prefix(request.Facet, entry.UniqueId);
        var profile = PersonProfileRules.Roll(uniqueId, definition);
        var basePersona = Brain.Personas?.Resolve(definition.Persona) ?? Persona.CreateNeutral();
        character.Persona = PersonaComposer.Compose(
            uniqueId,
            basePersona,
            PersonJobs.Of(definition.Build),
            Brain.PersonaParts,
            EraBands.Current(),
            profile.Traits
        );
        var gang = RedGangOf(request, entry);
        var red = gang != PkGangRules.NoGang;
        KeepRedHome(character, red);

        // A red's Den home is kept like a saved home; the identity then finds the Den's bank.
        character.BindIdentity(
            definition,
            map ?? character.Map,
            uniqueId,
            request.Facet,
            request.Content,
            alreadyInWorld || red,
            SideAtSpawn(uniqueId, definition, profile)
        );
        character.BindProfile(profile);
        BindTownLife(character, gang);
        EngineGuilds.Join(character);

        if (!alreadyInWorld)
        {
            character.ApplyFreshBuild();
        }

        character.ApplyOutlawStart();

        if (alreadyInWorld && red)
        {
            WakeRedAtHaven(character, map ?? character.Map);
        }

        character.EnsureFightingForm();
        CharacterLooks.Apply(character, uniqueId, definition.Name, profile, alreadyInWorld);
        character.BindVoice();

        if (!alreadyInWorld)
        {
            character.CompleteFreshStart();
        }
        else
        {
            Skills.RuneKit.Pack(character, firstDay: false);
        }

        character.EnsureLife();
        character.ScoreAndCommit();

        // A character still logged out is scored by its pulse once it stands in the world. One
        // waiting on its next-job answer has an action coming; only one with none is noted.
        if (character.Routine == null && character.Map != null && character.Map != Map.Internal)
        {
            if (!Brain.WaitsForJobChoice(character, Core.Now))
            {
                logger.Information("{Name} has no action and will idle ({Id})", character.Name, uniqueId);
            }

            character.IdleAtCurrentSpot();
        }

        if (alreadyInWorld)
        {
            logger.Information(
                "{Name} already exists at {Location} on {Map}, resumed {Action} ({Id})",
                character.Name,
                character.Location,
                character.Map,
                character.ActiveActionId ?? character.CurrentActivity,
                uniqueId
            );
        }
    }

    /// <summary>
    /// The Order or Chaos side a blue person wears from its first bind, so its home keeps
    /// the sides apart (<see cref="SideHomes"/>); a fixture's side follows its authored town.
    /// A red's home is the Den and does not ask.
    /// </summary>
    private static GuildType SideAtSpawn(string uniqueId, CharacterDefinition definition, PersonProfile profile) =>
        GuildCatalog.SideAtSpawn(
            uniqueId,
            profile.Class == PersonClass.Thief,
            BuildPresets.Resolve(definition.Build)?.IsFighter == true,
            SideHomes.HomeSide(uniqueId, definition.Spawn)
        );

    private static void BindTownLife(SosariaCharacter character, int gang)
    {
        var thief = character.PersonProfile.Class == PersonClass.Thief;

        EngineGuilds.Settle(character, thief, gang != PkGangRules.NoGang || PkRules.IsRed(character.Kills));
        ThievesGuild.Enroll(character, thief);
        character.ApplyPk(gang != PkGangRules.NoGang);
        character.OutlawGang = gang;
        RedGang.Enlist(character);
    }

    /// <summary>
    /// A red lives in Buccaneer's Den: it banks, shops and rests there, where no guard comes.
    /// A saved red keeps its place in the plan (<see cref="PkSlotRules.Select"/>), so a person
    /// the plan no longer makes red is one whose murders ran down, or one on a facet whose reds
    /// were turned off. It leaves the Den home it had, so the identity gives it an ordinary home
    /// again, and the murders go with the red name: a blue with a red count dies to the guards
    /// of its own town.
    /// </summary>
    private static void KeepRedHome(SosariaCharacter character, bool red)
    {
        if (red)
        {
            character.HomeSpawn = PkRules.BucsDenHaven;
        }
        else if (character.HomeSpawn == PkRules.BucsDenHaven)
        {
            character.HomeSpawn = Point3D.Zero;
            character.Kills = 0;
        }
    }

    /// <summary>
    /// A red saved in a guarded town would die to the guards the moment the world loads.
    /// Like a player logging back in at an inn, it wakes at its Den home instead. A red still
    /// waiting on <see cref="Map.Internal"/> for its return has its return spot moved.
    /// </summary>
    private static void WakeRedAtHaven(SosariaCharacter character, Map map)
    {
        var waiting = character.Map == Map.Internal && character.LogoutMap != null && character.LogoutMap != Map.Internal;
        var wakeMap = waiting ? character.LogoutMap : character.Map;
        var wakeAt = waiting ? character.LogoutLocation : character.Location;

        var rejects = new Dictionary<SpawnReject, int>();

        if (!GuardCall.IsGuardedPlace(wakeAt, wakeMap) ||
            !TryPlace(map, character.HomeFacet, character.HomeSpawn, character.HomeSpawn, character.CharacterId, rejects, out var haven, out _))
        {
            return;
        }

        logger.Information("{Name} is red and wakes at the Den, not under the guards at {Location}", character.Name, wakeAt);

        if (waiting)
        {
            character.LogoutMap = map;
            character.LogoutLocation = haven;
            return;
        }

        character.MoveToWorld(haven, map);
    }

    /// <summary>
    /// The red gang of this plan entry, or <see cref="PkGangRules.NoGang"/> for a blue. The
    /// facet's reds are chosen once per boot plan and kept for every entry after.
    /// </summary>
    private static int RedGangOf(FacetSpawnRequest request, SpawnPlanEntry entry)
    {
        if (!RedRosters.TryGetValue(request, out var roster))
        {
            roster = RedRoster(request);
            RedRosters.Add(request, roster);
        }

        foreach (var (id, gang) in roster)
        {
            if (IdMatches(id, entry.UniqueId, request.Facet))
            {
                return gang;
            }
        }

        return PkGangRules.NoGang;
    }

    private static List<(string Id, int Gang)> RedRoster(FacetSpawnRequest request)
    {
        var roster = new List<(string Id, int Gang)>();
        var maps = SosariaSettings.Characters?.Maps;
        var entries = request.Entries;

        if (maps == null ||
            !maps.TryGetValue(request.Facet, out var toggle) ||
            toggle == null ||
            !FacetNames.Felucca.Equals(request.Facet, StringComparison.OrdinalIgnoreCase) ||
            entries == null ||
            entries.Count == 0)
        {
            return roster;
        }

        var redCount = OutlawRules.RedCount(
            toggle.PkEnabled,
            toggle.PkCount,
            entries.Count,
            CharactersFile.DefaultFeluccaPkEnabled
        );

        if (redCount <= 0)
        {
            return roster;
        }

        var redsInSave = RedIdsInSave();
        var savedReds = new List<string>();
        var ids = new string[entries.Count];
        var isVeteran = new bool[entries.Count];
        var mayRideRed = new bool[entries.Count];
        var isPartyLeader = new bool[entries.Count];

        for (var i = 0; i < entries.Count; i++)
        {
            var slot = entries[i];
            ids[i] = slot.UniqueId;
            isPartyLeader[i] = IsPartyLeader(request, slot);

            if (redsInSave.Exists(saved => FacetIds.Matches(saved, request.Facet, slot.UniqueId)))
            {
                savedReds.Add(slot.UniqueId);
            }

            // The person the copy is, not the template it was made from: a copy of a
            // swordsman template can roll a miner, and a red miner walks the roads unarmed.
            var uniqueId = FacetIds.Prefix(request.Facet, slot.UniqueId);
            var definition = Person(request, slot.TemplateId, uniqueId);

            if (definition == null)
            {
                continue;
            }

            var build = BuildPresets.Resolve(definition.Build);
            var profile = PersonProfileRules.Roll(uniqueId, definition);
            var template = ClassBuilds.TemplateFor(profile.Class, build.Role, uniqueId);
            isVeteran[i] = build.Veteran;
            mayRideRed[i] = RedRosterRules.MayRideRed(PersonJobs.Of(definition.Build), template.Caster, template.TravelMagic, profile.Tier);
        }

        var selected = PkSlotRules.Select(ids, isVeteran, mayRideRed, isPartyLeader, savedReds, redCount);
        var gangPercent = toggle.PkGangPercent ?? PkGangRules.DefaultGangPercent;
        var gangs = RedRosterRules.RideTogether(PkGangRules.Gangs(selected, gangPercent));

        for (var i = 0; i < selected.Count; i++)
        {
            roster.Add((selected[i], gangs[i]));
        }

        var (gangCount, alone) = RedRosterRules.Count(gangs);
        console.Information(
            "{Facet} carries {Count} reds ({Kept} kept from the save) in {Gangs} gangs, {Alone} alone",
            request.Facet,
            selected.Count,
            savedReds.Count,
            gangCount,
            alone
        );
        return roster;
    }

    /// <summary>The ids of the people the world save holds with a red count.</summary>
    private static List<string> RedIdsInSave()
    {
        var ids = new List<string>();

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is SosariaCharacter { Deleted: false } character && PkRules.IsRed(character.Kills) &&
                !string.IsNullOrWhiteSpace(character.CharacterId))
            {
                ids.Add(character.CharacterId);
            }
        }

        return ids;
    }

    private static bool IsPartyLeader(FacetSpawnRequest request, SpawnPlanEntry entry)
    {
        var parties = request.Content?.Parties;

        if (parties == null)
        {
            return false;
        }

        for (var i = 0; i < parties.Count; i++)
        {
            var party = parties[i];

            if (party == null)
            {
                continue;
            }

            var leader = party.Leader;

            if (string.IsNullOrWhiteSpace(leader) &&
                IdMatches(party.Id, CharactersFile.PartyGraveyardCrew, request.Facet))
            {
                leader = PersonasFile.BranId;
            }

            if (IdMatches(leader, entry.UniqueId, request.Facet) ||
                IdMatches(leader, entry.TemplateId, request.Facet))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IdMatches(string left, string right, string facet)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return FacetIds.Matches(left, facet, right) || FacetIds.Matches(right, facet, left);
    }

    /// <summary>
    /// A tile near <paramref name="configured"/> on the site's floor that a person can spawn
    /// on and walk out of. Each test that turns a tile down is counted in <paramref name="rejects"/>.
    /// </summary>
    internal static bool TryResolveSpawnLocation(
        Map map,
        Point3D configured,
        Point3D site,
        Dictionary<SpawnReject, int> rejects,
        out Point3D location
    )
    {
        if (map == null || map == Map.Internal)
        {
            location = configured;
            return false;
        }

        // The site's real ground, not its written height: Magincia's gate is written at
        // z 0 and stands on a plateau at z 20 and more. The anchor is proven routable
        // once per site, so a standable tile only needs a straight walk to it — the
        // walk is itself a way out, and a walled yard fails it.
        var anchor = HomeSpotRules.SiteAnchor(map, site);

        if (HomeSpotRules.OpenNearby(map, configured, anchor, out location) is not { } first)
        {
            return true;
        }

        HomeSpotRules.Count(rejects, first);

        var walker = Standable.Walker(map);
        var standable = new List<Point3D>();

        for (var range = 1; range <= SpawnSearchRange; range++)
        {
            for (var dx = -range; dx <= range; dx++)
            {
                for (var dy = -range; dy <= range; dy++)
                {
                    if (Math.Abs(dx) != range && Math.Abs(dy) != range)
                    {
                        continue;
                    }

                    var x = configured.X + dx;
                    var y = configured.Y + dy;

                    if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
                    {
                        continue;
                    }

                    if (HomeSpotRules.FloorSpot(walker, x, y, anchor) is not { } candidate)
                    {
                        HomeSpotRules.Count(rejects, SpawnReject.NoFloor);
                    }
                    else if (!map.CanSpawnMobile(candidate))
                    {
                        HomeSpotRules.Count(rejects, SpawnReject.Blocked);
                    }
                    else
                    {
                        standable.Add(candidate);
                    }
                }
            }
        }

        if (HomeSpotRules.AnchorRoutable(map, anchor))
        {
            for (var i = 0; i < standable.Count; i++)
            {
                if (WalkLine.Reaches(walker, standable[i], anchor))
                {
                    location = standable[i];
                    return true;
                }

                HomeSpotRules.Count(rejects, SpawnReject.NoWalkLine);
            }
        }

        // A straight walk can fail from a legal tile that needs a corner turn. Give a
        // bounded few the full route proof before giving the spawn up.
        var proofs = Math.Min(FallbackProofs, standable.Count);

        for (var i = 0; i < proofs; i++)
        {
            if (HomeSpotRules.Routable(map, standable[i]))
            {
                location = standable[i];
                return true;
            }

            HomeSpotRules.Count(rejects, SpawnReject.Sealed);
        }

        location = configured;
        return false;
    }
}
