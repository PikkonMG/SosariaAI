using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Logging;

namespace SosariaAI.Skills;

/// <summary>How a step through a gate went this think.</summary>
public enum GateStep
{
    /// <summary>The person is on the far side.</summary>
    Through,

    /// <summary>Still stepping onto the pad, or resting before the next moon hop.</summary>
    Waiting,

    /// <summary>No gate here, or the gate would not take the person.</summary>
    Refused
}

/// <summary>
/// Gates as players use them. A character is a player now, so a <see cref="Teleporter"/>
/// carries it the moment it steps onto the pad, by the pad's own rules. A spell gate takes
/// it when it walks in and answers the confirm gump. A public moongate opens a destination
/// gump no character can click, so its hop is the answer to that gump.
/// </summary>
public static class GateTravel
{
    private static readonly ILogger logger = SosariaLog.For(typeof(GateTravel));
    private static readonly Dictionary<Serial, DateTime> LastHop = new();

    /// <summary>A walk with this reach ends on the pad itself (<see cref="CharacterMotor.MoveToPoint"/>).</summary>
    private const int OntoPadRange = 0;

    public const int MoongateOpenSound = 0x20E;
    public const int MoongateArriveSound = 0x1FE;

    /// <summary>
    /// True when <paramref name="pad"/> is a live teleporter on <paramref name="map"/> that
    /// would carry <paramref name="walker"/> somewhere on the same map, by the pad's own rules.
    /// </summary>
    public static bool IsUsablePad(Teleporter pad, Map map, Mobile walker) =>
        pad is { Deleted: false, Active: true } && pad.PointDest != Point3D.Zero &&
        (pad.MapDest == null || pad.MapDest == map) && pad.CanTeleport(walker);

    /// <summary>
    /// Steps onto the teleporter pad that goes where the plan goes; the engine moves the
    /// person. A pad the person already stands on did not fire, so it steps off onto a tile
    /// with no pad and back on (<see cref="GatePad.StepOffTile"/>);
    /// a pad that fires for no step onto it (<see cref="GatePad.NeverFires(TileWalker, Point3D, int)"/>)
    /// refuses at once. Beside the pad the person takes the step onto it that the engine allows
    /// (<see cref="GatePad.EntryStep"/>), round a blocked corner first; with none, it walks onto
    /// the pad by a path. The walkers set down at (2400,199) asked the engine for the diagonal
    /// onto the pad at (2399,198) until their tries ran out, and 54 trips ended there. Without a
    /// pad nobody moves: appearing across the map with no gate is not something a player could do.
    /// </summary>
    public static GateStep StepThroughTeleporter(SosariaCharacter character, Point3D plannedLanding, string eventName)
    {
        if (!People.InWorld(character))
        {
            return GateStep.Refused;
        }

        if (GateHopRules.Landed(character.Location, plannedLanding))
        {
            NoteThrough(character, eventName, plannedLanding);
            return GateStep.Through;
        }

        var walker = Standable.Walker(character.Map);
        var pad = FindTeleporter(character, plannedLanding, walker);

        if (pad == null)
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Line}", NoGateLine(character.Name, character.Location));
            }

            return GateStep.Refused;
        }

        // A pad no step sets off only ever has the person step on and off it.
        if (GatePad.NeverFires(walker, pad.Location, pad.ItemData.Height))
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Line}", DeadPadLine(character.Name, pad.Location));
            }

            return GateStep.Refused;
        }

        var from = character.Location;
        var fromMap = character.Map;
        var distance = NavMetric.Chebyshev(from, pad.Location);

        if (distance > 1)
        {
            character.Motor.MoveToPoint(pad);
            return GateStep.Waiting;
        }

        if (distance == 0)
        {
            StepOffPad(character, walker);
            return GateStep.Waiting;
        }

        // The pad takes along only pets close by: a tamer steps on once they have caught up.
        if (PetKeeper.HoldsForPets(character))
        {
            return GateStep.Waiting;
        }

        if (GatePad.EntryStep(walker, from, pad.Location) is { } next)
        {
            character.Motor.DoMove(character.GetDirectionTo(next));
        }
        else
        {
            character.Motor.MoveToPoint(pad, OntoPadRange);
        }

        if (!GateHopRules.Carried(from, character.Location, fromMap == character.Map))
        {
            return GateStep.Waiting;
        }

        NoteThrough(character, eventName, character.Location);
        return GateStep.Through;
    }

    private static void StepOffPad(SosariaCharacter character, TileWalker walker)
    {
        var map = character.Map;

        if (GatePad.StepOffTile(
                walker,
                character.Location,
                (x, y, z) => GatePad.IsUnder(map, new Point3D(x, y, z), NavGateKind.Teleporter)
            ) is { } off)
        {
            character.Motor.DoMove(character.GetDirectionTo(off));
        }
    }

    /// <summary>
    /// A spell gate near here that lands near <paramref name="toward"/>: the gate a party
    /// leader just walked through. Null when there is none.
    /// </summary>
    public static Moongate FindSpellGateToward(SosariaCharacter character, Point3D toward, Map towardMap)
    {
        if (!People.InWorld(character) || towardMap == null)
        {
            return null;
        }

        foreach (var gate in character.Map.GetItemsInRange<Moongate>(character.Location, GateHopRules.SpellGateSearchTiles))
        {
            if (gate is { Deleted: false } &&
                gate.TargetMap == towardMap &&
                GateHopRules.GateLandsNear(gate.Target, toward))
            {
                return gate;
            }
        }

        return null;
    }

    /// <summary>
    /// Walks into a spell gate and answers its confirm gump, as a player clicks OK. The
    /// gate's own use rules move the person. Beside the gate it waits while it fights, is
    /// gray, or is in the heat of battle (<see cref="MayStepThrough(SosariaCharacter)"/>): the
    /// engine's spell gate asks none of it, so without this a party member hit a moment before
    /// stepped through the caster's gate. A gate that closes meanwhile refuses.
    /// </summary>
    public static GateStep StepIntoSpellGate(SosariaCharacter character, Moongate gate, string eventName)
    {
        if (character?.Map == null || gate == null || gate.Deleted || gate.Map != character.Map)
        {
            return GateStep.Refused;
        }

        if (!character.InRange(gate.Location, 1))
        {
            character.Motor.MoveToPoint(gate);
            return GateStep.Waiting;
        }

        if (!MayStepThrough(character))
        {
            return GateStep.Waiting;
        }

        var from = character.Location;
        var fromMap = character.Map;
        gate.EndConfirmation(character);

        if (!GateHopRules.Carried(from, character.Location, fromMap == character.Map))
        {
            return GateStep.Refused;
        }

        NoteThrough(character, eventName, character.Location);
        return GateStep.Through;
    }

    private static void NoteThrough(SosariaCharacter character, string eventName, Point3D dest)
    {
        Log(character, eventName, dest);
        DungeonGate.NoteWhere(character);
    }

    /// <summary>
    /// A murderer, alive or a ghost, takes a public moongate on Felucca when neither its pad
    /// nor the pad it lands on stands in a guarded region: the guards there kill a red on
    /// sight, so the engine's own regions decide, pad by pad. Off Felucca it never does. A red
    /// ghost is held to it too, as its roads are (<see cref="PathSearchBars.For"/>): a red
    /// ghost that lands on a guarded pad is raised there and dies to the guards. The Den has
    /// its own healer on foot, and the unguarded pads bring a red ghost from afar.
    /// </summary>
    public static bool MayTakeMoongate(bool murderer, bool felucca, bool originGuarded, bool destinationGuarded) =>
        !murderer || felucca && !originGuarded && !destinationGuarded;

    /// <summary>This person may take a public moongate from <paramref name="origin"/> to <paramref name="destination"/>.</summary>
    public static bool MayTakeMoongate(SosariaCharacter character, Point3D origin, Point3D destination, Map destinationMap)
    {
        var murderer = PkRules.IsRed(character.Kills);

        // Only a murderer's hop asks the regions.
        return MayTakeMoongate(
            murderer,
            character.Map == Map.Felucca && destinationMap == Map.Felucca,
            murderer && GuardCall.IsGuardedPlace(origin, character.Map),
            murderer && GuardCall.IsGuardedPlace(destination, destinationMap)
        );
    }

    /// <summary>Every moongate hop of a plan is one this person may take.</summary>
    public static bool MayTakeMoongates(SosariaCharacter character, IReadOnlyList<TravelStep> steps)
    {
        for (var i = 1; i < (steps?.Count ?? 0); i++)
        {
            if (steps[i].ArrivalGate == NavGateKind.Moongate &&
                !MayTakeMoongate(character, steps[i - 1].Location, steps[i].Location, character.Map))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A person steps through a gate, public or spell, only with no living foe on it, not
    /// gray, and out of the heat of battle (<see cref="TravelHeat"/>): nobody takes a moongate,
    /// recall or gate within half a minute of a blow on or from a player. Pure.
    /// </summary>
    public static bool MayStepThrough(bool fighting, bool criminal, bool inHeat) =>
        DefendRules.MayTakeGate(fighting) && TravelHeat.MayTakeMoongate(criminal, inHeat);

    /// <summary>This person may step through a gate now (<see cref="MayStepThrough(bool, bool, bool)"/>).</summary>
    public static bool MayStepThrough(SosariaCharacter character) =>
        MayStepThrough(
            character.Combatant is { Deleted: false, Alive: true },
            character.Criminal,
            TravelHeat.Hot(character)
        );

    /// <summary>The line a public moongate hop writes, easy to count.</summary>
    public static string MoongateLine(string name, Point3D from, Point3D to) =>
        $"{name} took a moongate from {from} to {to}";

    /// <summary>
    /// One hop through a public moongate: the pad the person stands on, or one in reach,
    /// carries it to <paramref name="dest"/>. A wait while the moon rests, the person fights, is
    /// gray, or is still in the heat of battle (<see cref="TravelHeat"/>), as for a player.
    /// Refused when no pad lies in reach, and for a murderer bound to or from a guarded pad:
    /// a missing pad counted as a wait, and a person who fled from the Trinsic pad asked the
    /// empty ground for it several times a second for four hours.
    /// </summary>
    public static GateStep ApplyMoongate(SosariaCharacter character, Point3D dest, Map destMap, Point3D? exitToward = null)
    {
        if (character == null || destMap == null || destMap == Map.Internal ||
            !MayTakeMoongate(character, character.Location, dest, destMap))
        {
            return GateStep.Refused;
        }

        var sameMap = character.Map == destMap;

        if (!MayStepThrough(character))
        {
            return GateStep.Waiting;
        }

        if (!GateHopRules.ShouldHop(character.Location, dest, sameMap))
        {
            return GateStep.Through;
        }

        if (!GateHopRules.MayHop(LastHopAt(character.Serial), Core.Now))
        {
            return GateStep.Waiting;
        }

        if (!OnMoongate(character) && !StepOntoNearbyMoongate(character))
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Line}", NoGateLine(character.Name, character.Location));
            }

            return GateStep.Refused;
        }

        var from = character.Location;
        var fromMap = character.Map;

        Effects.PlaySound(from, fromMap, MoongateOpenSound);
        Effects.SendLocationEffect(from, fromMap, SosariaCombat.TeleporterEffectId, SosariaCombat.TeleporterEffectDuration);
        character.MoveToWorld(dest, destMap);
        var off = FindMoongateExit(
            character.Location,
            destMap,
            exitToward ?? dest,
            unchecked((int)character.Serial.Value)
        );
        character.MoveToWorld(off, destMap);
        Effects.PlaySound(character.Location, character.Map, MoongateArriveSound);
        Effects.SendLocationEffect(
            character.Location,
            character.Map,
            SosariaCombat.TeleporterEffectId,
            SosariaCombat.TeleporterEffectDuration
        );

        LastHop[character.Serial] = Core.Now;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", MoongateLine(character.Name, from, character.Location));
        }

        return GateStep.Through;
    }

    /// <summary>
    /// Cells the tile proofs of one hop's exits share: each is a world-thread search, and
    /// twenty-five of them, each a full tile route, could run for one hop.
    /// </summary>
    public const int ExitReachCells = TileRoute.WorldMaxCells;

    /// <summary>
    /// The tile a person steps off a moongate onto: the least crowded one ahead of the pad,
    /// toward the next leg, that walks on there. The pad itself when none does.
    /// </summary>
    private static Point3D FindMoongateExit(Point3D pad, Map map, Point3D toward, int seed)
    {
        var dx = Math.Sign(toward.X - pad.X);
        var dy = Math.Sign(toward.Y - pad.Y);

        if (dx == 0 && dy == 0)
        {
            dx = 1;
        }

        var sideX = -dy;
        var sideY = dx;
        var best = Point3D.Zero;
        var bestCrowd = int.MaxValue;
        var proofCells = new TileRoute.CellAllowance(ExitReachCells);

        for (var distance = GateHopRules.StepOffTiles; distance <= 6; distance++)
        {
            for (var offset = -2; offset <= 2; offset++)
            {
                var shifted = offset + seed % 3;
                var candidate = new Point3D(
                    pad.X + dx * distance + sideX * shifted,
                    pad.Y + dy * distance + sideY * shifted,
                    pad.Z
                );

                if (!TryExitAt(map, candidate, out candidate))
                {
                    continue;
                }

                if (NeedsFootProof(candidate, toward) && !ReachesOnFoot(map, candidate, toward, proofCells))
                {
                    continue;
                }

                var crowd = CrowdAt(map, candidate);

                if (crowd < bestCrowd)
                {
                    best = candidate;
                    bestCrowd = crowd;
                }

                if (crowd == 0)
                {
                    return candidate;
                }
            }
        }

        return best != Point3D.Zero ? best : pad;
    }

    /// <summary>True when an exit's walk on to <paramref name="toward"/> is proved on tiles: a goal past the tile router's reach is left to the roads.</summary>
    public static bool NeedsFootProof(Point3D from, Point3D toward) =>
        toward != Point3D.Zero && NavMetric.Chebyshev(from, toward) <= TileRoute.MaxTripTiles;

    /// <summary>The walk from an exit on to <paramref name="toward"/>, paid from <paramref name="cells"/>; none once they are spent.</summary>
    private static bool ReachesOnFoot(Map map, Point3D from, Point3D toward, TileRoute.CellAllowance cells) =>
        TileRoute.Find(
            from,
            toward,
            Standable.Walker(map),
            (x, y, z) => IndoorTiles.IsBuilding(map, x, y, z),
            cells
        ).Count > 0;

    /// <summary>
    /// Where a person fits at <paramref name="candidate"/>: the spot itself, else the ground
    /// under it. False with zero when neither takes a person.
    /// </summary>
    public static bool TryExitAt(Map map, Point3D candidate, out Point3D exit)
    {
        if (map.CanSpawnMobile(candidate))
        {
            exit = candidate;
            return true;
        }

        candidate = new Point3D(candidate.X, candidate.Y, map.GetAverageZ(candidate.X, candidate.Y));

        if (map.CanSpawnMobile(candidate))
        {
            exit = candidate;
            return true;
        }

        exit = Point3D.Zero;
        return false;
    }

    private static int CrowdAt(Map map, Point3D at)
    {
        var crowd = 0;

        foreach (var mobile in map.GetMobilesInRange(at, 2))
        {
            if (mobile is SosariaCharacter { Deleted: false, Alive: true })
            {
                crowd++;
            }
        }

        return crowd;
    }

    private static DateTime LastHopAt(Serial serial) =>
        LastHop.TryGetValue(serial, out var at) ? at : default;

    /// <summary>One think of stepping through a graph gate. Refused moves nobody.</summary>
    public static GateStep Apply(
        SosariaCharacter character,
        NavGateKind kind,
        Point3D dest,
        Map destMap,
        string eventName,
        Point3D? exitToward = null
    ) =>
        kind switch
        {
            NavGateKind.Teleporter => StepThroughTeleporter(character, dest, eventName),
            NavGateKind.Moongate => ApplyMoongate(character, dest, destMap, exitToward),
            // Boat is kept only so a graph file from an earlier build cannot move anyone.
            // There is no ship, and a character appearing on a far dock is not something a
            // player could do. Moongates already reach every island town.
            _ => GateStep.Refused
        };

    public static string NoGateLine(string name, Point3D at) => $"{name} found no gate at ({at.X},{at.Y})";

    /// <summary>The line a pad that never fires writes, easy to count.</summary>
    public static string DeadPadLine(string name, Point3D pad) =>
        $"{name} found the teleporter at ({pad.X},{pad.Y},{pad.Z}) fires for no step onto it";

    private static Teleporter FindTeleporter(SosariaCharacter character, Point3D plannedLanding, TileWalker walker)
    {
        var gates = new List<Teleporter>();
        var pads = new List<GatePad.Pad>();

        foreach (var item in character.Map.GetItemsInRange<Teleporter>(character.Location, GatePad.ReachTiles))
        {
            gates.Add(item);
            pads.Add(new GatePad.Pad(
                item.Location,
                item.PointDest,
                item.MapDest == null || item.MapDest == character.Map,
                GatePad.SomeStepReaches(walker, item.Location, item.ItemData.Height),
                !GatePad.NeverFires(walker, item.Location, item.ItemData.Height)
            ));
        }

        var index = GatePad.Pick(character.Location, plannedLanding, pads);
        return index < 0 ? null : gates[index];
    }

    private static bool OnMoongate(SosariaCharacter character) =>
        character?.Map != null &&
        character.Map != Map.Internal &&
        GatePad.IsUnder(character.Map, character.Location, NavGateKind.Moongate);

    private static bool StepOntoNearbyMoongate(SosariaCharacter character)
    {
        if (!People.InWorld(character))
        {
            return false;
        }

        PublicMoongate nearest = null;
        var best = int.MaxValue;

        foreach (var gate in character.Map.GetItemsInRange<PublicMoongate>(
                     character.Location,
                     GatePad.ReachTiles
                 ))
        {
            if (gate == null || !GatePad.IsInReach(character.Location, gate.Location))
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(character.Location, gate.Location);

            if (distance >= best)
            {
                continue;
            }

            nearest = gate;
            best = distance;
        }

        if (nearest == null)
        {
            return false;
        }

        character.MoveToWorld(nearest.Location, character.Map);
        return true;
    }

    private static void Log(SosariaCharacter character, string eventName, Point3D dest)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} {Event} at {Location}", character.Name, eventName, dest);
        }
    }
}
