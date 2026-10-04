using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using Server.Guilds;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// Order against Chaos in the world: where a fight may start, the words said when one may
/// not, the draft up to four a side, and the break-off to heal. The war bands that ride out to the meeting spots are <see cref="PartyRoads"/>, and
/// the rare small fights on town streets are <see cref="TownScuffles"/>. The
/// rules are in <see cref="FactionRules"/>. In memory only.
/// </summary>
public static class FactionWar
{
    /// <summary>Once this many pairs are held, the pairs whose rest ran out are dropped.</summary>
    public const int SweepAt = 256;

    private static readonly Dictionary<Serial, DateTime> LastTaunt = new();
    private static readonly Dictionary<(Serial, Serial), DateTime> LastPairTaunt = new();
    private static readonly Dictionary<Serial, DateTime> LastTauntOn = new();
    private static readonly Dictionary<(string Facet, string Place), DateTime> LastTauntIn = new();
    private static readonly Dictionary<Serial, DateTime> SkirmishStart = new();
    private static readonly ConditionalWeakTable<DestinationCatalog, PeacePlace[]> PeacePlaces = new();

    /// <summary>Order against Chaos, or two guilds at war: a fight between them is no crime.</summary>
    public static bool AreFoes(Mobile first, Mobile second) =>
        EngineGuilds.Opposed(first, second) || EngineGuilds.AtWar(first, second);

    /// <summary>An agreed duel, Order against Chaos, or a guild war: no crime, no gray, no guards.</summary>
    public static bool LawfulFight(Mobile first, Mobile second) =>
        first != null && second != null && (Duels.AreFighting(first, second) || AreFoes(first, second));

    /// <summary>Under the guards, or near a bank, a healer, a moongate or a shrine.</summary>
    public static bool InSafePlace(Mobile mobile) => InSafePlace(mobile, FactionRules.SafeRadius);

    /// <summary>
    /// Clear of a place of peace by <see cref="FactionRules.FightRoomRadius"/> and off the guard
    /// line (<see cref="GuardCall.AtGuardLine"/>): a fight started here does not cross into one
    /// with its first steps.
    /// </summary>
    public static bool HasRoomToFight(Mobile mobile) =>
        !InSafePlace(mobile, FactionRules.FightRoomRadius) && !GuardCall.AtGuardLine(mobile.Location, mobile.Map);

    /// <summary>
    /// The ground lets these two fight (<see cref="FactionRules.MayFightAt"/>): Order against
    /// Chaos anywhere, a guild war only with room on both sides.
    /// </summary>
    public static bool GroundAllows(Mobile self, Mobile foe) =>
        FactionRules.MayFightAt(EngineGuilds.Opposed(self, foe), HasRoomToFight(self), HasRoomToFight(foe));

    /// <summary>A character with its combat kit and its weapon (<see cref="SpareKit.Armed"/>); a real player always counts as armed.</summary>
    public static bool Armed(Mobile mobile) => mobile is not SosariaCharacter character || SpareKit.Armed(character);

    /// <summary>A person who stood down at the line inside the grace: no new foe draws on it, and it draws on none.</summary>
    public static bool StoppedAtLineLately(Mobile mobile) => mobile is SosariaCharacter { StoppedAtLineLately: true };

    private static bool InSafePlace(Mobile mobile, int radius)
    {
        var map = mobile?.Map;

        return map == null || map == Map.Internal || SosariaCharacter.UnderGuards(mobile) ||
               NearPeacePlace(mobile, radius, radius);
    }

    /// <summary>
    /// A bank inside <paramref name="bankRadius"/>, or a healer, a shrine or a public moongate
    /// inside <paramref name="otherRadius"/>. A town scuffle keeps a wider ring round a bank
    /// (<see cref="TownScuffles"/>) than the road fights do.
    /// </summary>
    public static bool NearPeacePlace(Mobile mobile, int bankRadius, int otherRadius) =>
        mobile != null && NearPeacePlace(mobile.Location, mobile.Map, bankRadius, otherRadius);

    /// <summary>The same test for a spot on a map: a town scuffle's street spot is chosen with it.</summary>
    public static bool NearPeacePlace(Point3D at, Map map, int bankRadius, int otherRadius)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        var places = PeacePlacesOf(NavWorld.DestinationsFor(map.Name));

        for (var i = 0; i < places.Length; i++)
        {
            if (FactionRules.NearPeace(NavMetric.Chebyshev(at, places[i].Location), places[i].IsBank, bankRadius, otherRadius))
            {
                return true;
            }
        }

        foreach (var _ in map.GetItemsInRange<PublicMoongate>(at, otherRadius))
        {
            return true;
        }

        return false;
    }

    /// <summary>The one who killed this person, or whom it killed, inside the revenge rest.</summary>
    public static bool Feuding(SosariaCharacter self, Mobile foe)
    {
        var other = foe as SosariaCharacter;
        return FactionRules.Feuding(
            self.LastKillerName,
            foe.Name,
            other?.LastKillerName,
            self.Name,
            self.LastDeathAt,
            other?.LastDeathAt ?? default,
            Core.Now
        );
    }

    /// <summary>
    /// A foe this person may draw on: not freshly dead, not the one it feuds with, not one it just
    /// walked away from nor of the pack it ran from lately, not met too lately, on ground that
    /// allows the fight (<see cref="GroundAllows"/>), both armed (<see cref="FactionRules.MayFight"/>),
    /// neither stood down at the line lately
    /// (<see cref="StoppedAtLineLately"/>),
    /// for a blue not in Buccaneer's Den off a raid (<see cref="WorldPlay.MayStartFightInDen"/>),
    /// its own side under four there, and not a fight it would run from at once
    /// (<see cref="WorldPlay.WouldRunFrom"/>). Bevis drew on a guild enemy, said "too many" and
    /// ran in the same second, got clear a few tiles on and drew again, all half an hour.
    /// </summary>
    public static bool MayEngage(SosariaCharacter self, Mobile foe, Func<Mobile, bool> onSide)
    {
        var now = Core.Now;

        if (foe is SosariaCharacter fallen && !WorldPlay.OutOfDeathGrace(fallen.LastDeathAt, now))
        {
            return false;
        }

        return !Feuding(self, foe) &&
               !WorldPlay.LeavesBeSide(self, foe) &&
               WorldPlay.PairReady(self.Serial, foe.Serial, now) &&
               GroundAllows(self, foe) && FactionRules.MayFight(Armed(self), Armed(foe)) &&
               !self.StoppedAtLineLately && !StoppedAtLineLately(foe) &&
               WorldPlay.MayStartFightInDen(self, foe) &&
               FactionRules.MayJoinSide(SideFighters(foe.Map, foe.Location, onSide)) &&
               !WorldPlay.WouldRunFrom(self, foe, FoeOf(self));
    }

    /// <summary>The side test for everyone this person is at war with: the other alignment, or a guild at war with its own.</summary>
    public static Func<Mobile, bool> FoeOf(Mobile self) => mobile => AreFoes(self, mobile);

    /// <summary>Order or Chaos: the side test for a skirmish count.</summary>
    public static Func<Mobile, bool> AlignmentSide(GuildType side) => mobile => EngineGuilds.AlignmentOf(mobile) == side;

    /// <summary>One guild: the side test for a guild war skirmish count.</summary>
    public static Func<Mobile, bool> GuildSide(BaseGuild guild) => mobile => guild != null && mobile.Guild == guild;

    /// <summary>People of one side near a spot who are fighting a foe of theirs there.</summary>
    public static int SideFighters(Map map, Point3D center, Func<Mobile, bool> onSide)
    {
        if (map == null || map == Map.Internal)
        {
            return 0;
        }

        var count = 0;

        foreach (var mobile in map.GetMobilesInRange(center, FactionRules.DraftRange))
        {
            if (mobile is PlayerMobile { Alive: true } person && onSide(person) &&
                person.Combatant is { Deleted: false, Alive: true } foe && AreFoes(person, foe))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// A meeting that may not come to blows (a pair resting from each other, a side already
    /// four strong, odds it would run from) gets words instead: once per person, pair, foe and
    /// place in their rests.
    /// </summary>
    public static bool TryTaunt(SosariaCharacter character, Mobile foe, GuildType side)
    {
        var now = Core.Now;
        var pair = WorldPlay.PairKey(character.Serial, foe.Serial);
        var place = (character.Map.Name, PlaceNames.RegionOf(character));

        if (!FactionRules.TauntDue(
                LastTaunt.GetValueOrDefault(character.Serial),
                LastPairTaunt.GetValueOrDefault(pair),
                LastTauntOn.GetValueOrDefault(foe.Serial),
                LastTauntIn.GetValueOrDefault(place),
                now
            ))
        {
            return false;
        }

        if (LastPairTaunt.Count >= SweepAt)
        {
            Sweep(LastPairTaunt, now, FactionRules.PairTauntRest);
            Sweep(LastTauntOn, now, FactionRules.FoeTauntRest);
            Sweep(LastTauntIn, now, FactionRules.PlaceTauntRest);
        }

        LastTaunt[character.Serial] = now;
        LastPairTaunt[pair] = now;
        LastTauntOn[foe.Serial] = now;
        LastTauntIn[place] = now;
        Talk.Say(character, TauntCategory(side), new TalkSlots { Foe = foe.Name });
        WorldPlay.Log($"{character.Name} ({side}) jeered at {foe.Name}");
        return true;
    }

    public static string TauntCategory(GuildType side) =>
        side == GuildType.Chaos ? TalkCategory.ChaosTaunt : TalkCategory.OrderTaunt;

    /// <summary>
    /// When shields meet, idle faction-mates nearby are drafted in on both sides, up to four a
    /// side. The foe's side answers too, so a clash is a skirmish, not a lynching.
    /// </summary>
    public static void Draft(SosariaCharacter caller, Mobile foe)
    {
        DraftSide(caller, foe, EngineGuilds.AlignmentOf(caller));
        DraftSide(foe, caller, EngineGuilds.AlignmentOf(foe));
    }

    /// <summary>
    /// A guild war draw is a skirmish too: idle guildmates of both sides near are drafted in,
    /// up to four a side. The run test before the draw counts them (<see cref="CombatBrain.WouldRunFrom"/>);
    /// alone, the drawer met the whole enemy guild and ran "too many" in the same second.
    /// </summary>
    public static void DraftGuild(SosariaCharacter caller, Mobile foe)
    {
        DraftSide(caller, foe, GuildSide(caller.Guild), caller.Guild?.Abbreviation);
        DraftSide(foe, caller, GuildSide(foe.Guild), foe.Guild?.Abbreviation);
    }

    /// <summary>
    /// A mate free to join a fight on <paramref name="target"/>: alive, a fighter by trade, in no
    /// fight, run or duel, in sight of it, out of the death grace and the revenge rest, not of the
    /// pack it ran from, rested from that foe, on ground that allows the fight
    /// (<see cref="GroundAllows"/>), armed (<see cref="Armed"/>) and not stood down at the line lately, and for a blue not in Buccaneer's
    /// Den off a raid (<see cref="WorldPlay.MayStartFightInDen"/>). The draft takes these, and the
    /// run test before a draw counts them (<see cref="CombatBrain.WouldRunFrom"/>). A crafter or a
    /// thief drafted in ran at the first blow.
    /// </summary>
    public static bool FreeToJoin(SosariaCharacter mate, Mobile target) =>
        mate.Alive && !mate.IsGhost && mate.Build?.IsFighter == true && mate.Combatant == null &&
        !mate.CheckFlee() && Duels.Find(mate) == null && mate.CanSee(target) &&
        WorldPlay.OutOfDeathGrace(mate.LastDeathAt, Core.Now) && !Feuding(mate, target) &&
        !WorldPlay.LeavesBeSide(mate, target) &&
        WorldPlay.PairReady(mate.Serial, target.Serial, Core.Now) && GroundAllows(mate, target) && Armed(mate) && !mate.StoppedAtLineLately &&
        WorldPlay.MayStartFightInDen(mate, target);

    /// <summary>
    /// A fighter in a skirmish breaks off when badly hurt or when the skirmish ran long, runs a
    /// moment, and then heals. The foe it faced stands down with it. The run is a decided one
    /// (<see cref="CombatBrain.RunDecided"/>): it is clear only with that foe well off. Without
    /// the foe as its source, two in three break-offs got clear within three seconds.
    /// </summary>
    public static void ConsiderDisengage(SosariaCharacter character)
    {
        if (character.Combatant is not PlayerMobile { Deleted: false, Alive: true } foe || !AreFoes(character, foe))
        {
            SkirmishStart.Remove(character.Serial);
            return;
        }

        var now = Core.Now;

        if (!SkirmishStart.TryGetValue(character.Serial, out var start))
        {
            SkirmishStart[character.Serial] = now;
            return;
        }

        var hits = Vitals.HitsFraction(character);

        if (!FactionRules.ShouldDisengage(hits, Vitals.HitsFraction(foe), start, now))
        {
            return;
        }

        SkirmishStart.Remove(character.Serial);
        character.StandDown();

        if (FactionRules.StandsFoeDown(hits) && foe is SosariaCharacter other && other.Combatant == character)
        {
            other.StandDown();
        }

        CombatBrain.RunDecided(character, foe, FactionRules.DisengageRun);
        WorldPlay.Log($"{character.Name} broke off from {foe.Name} to heal");
    }

    /// <summary>The nearest enemy of the other side in the open that this patrol may set a course on.</summary>
    public static Mobile NearestQuarry(SosariaCharacter character, GuildType side)
    {
        var map = character.Map;

        if (map == null || map == Map.Internal || !WorldPlay.RallyReady(character.Serial, Core.Now))
        {
            return null;
        }

        Mobile best = null;
        var bestDistance = int.MaxValue;

        foreach (var mobile in map.GetMobilesInRange(character.Location, FactionRules.InterceptRange))
        {
            if (mobile is not PlayerMobile { Alive: true, Hidden: false } foe || !EngineGuilds.Opposed(character, foe) ||
                !character.CanSee(foe))
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(character.Location, foe.Location);

            if (distance < bestDistance && MayEngage(character, foe, AlignmentSide(side)))
            {
                best = foe;
                bestDistance = distance;
            }
        }

        return best;
    }

    // Idle mates of Order or Chaos near the ally join against the target, up to four a side.
    private static void DraftSide(Mobile ally, Mobile target, GuildType side)
    {
        if (side != GuildType.Regular)
        {
            DraftSide(ally, target, AlignmentSide(side), side.ToString());
        }
    }

    // Idle mates of the ally's side near it join against the target, up to four a side.
    private static void DraftSide(Mobile ally, Mobile target, Func<Mobile, bool> onSide, string sideName)
    {
        var map = ally.Map;

        if (map == null || map == Map.Internal)
        {
            return;
        }

        var slots = FactionRules.DraftSlots(SideFighters(map, ally.Location, onSide));
        var drafted = new List<SosariaCharacter>();

        foreach (var mobile in map.GetMobilesInRange(ally.Location, FactionRules.DraftRange))
        {
            if (drafted.Count >= slots)
            {
                break;
            }

            if (mobile is SosariaCharacter mate && mate != ally && onSide(mate) && MayDraft(mate, target))
            {
                drafted.Add(mate);
            }
        }

        for (var i = 0; i < drafted.Count; i++)
        {
            var mate = drafted[i];
            mate.JoinAgainst(target);
            WorldPlay.NoteFight(mate, target);
            WorldPlay.Log($"{mate.Name} ({sideName}) is drafted in against {target.Name}");
        }
    }

    private static bool MayDraft(SosariaCharacter mate, Mobile target) =>
        FreeToJoin(mate, target) && !WorldPlay.WouldRunFrom(mate, target, FoeOf(mate));

    // Banks, healers and shrines of a facet, read once per catalog.
    private static PeacePlace[] PeacePlacesOf(DestinationCatalog catalog)
    {
        if (catalog == null)
        {
            return [];
        }

        return PeacePlaces.GetValue(
            catalog,
            source =>
            {
                var places = new List<PeacePlace>();

                for (var i = 0; i < source.All.Count; i++)
                {
                    var destination = source.All[i];
                    var kind = destination.ParsedKind;

                    if (kind is DestinationKind.Bank or DestinationKind.Healer or DestinationKind.Shrine)
                    {
                        places.Add(new PeacePlace(destination.Arrival, kind == DestinationKind.Bank));
                    }
                }

                return places.ToArray();
            }
        );
    }

    // A bank, a healer or a shrine, and whether it is the bank.
    private readonly record struct PeacePlace(Point3D Location, bool IsBank);

    /// <summary>Drops the marks whose rest ran out.</summary>
    public static void Sweep<TKey>(Dictionary<TKey, DateTime> marks, DateTime now, TimeSpan rest)
    {
        List<TKey> old = null;

        foreach (var (key, at) in marks)
        {
            if (TimeRules.Rested(at, now, rest))
            {
                (old ??= []).Add(key);
            }
        }

        for (var i = 0; old != null && i < old.Count; i++)
        {
            marks.Remove(old[i]);
        }
    }
}
