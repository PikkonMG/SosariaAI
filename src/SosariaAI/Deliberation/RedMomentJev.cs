using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Deliberation;

/// <summary>What a red just raised next to its body knows, read on the world thread.</summary>
public readonly record struct WayBackFacts(
    bool Armed,
    int BodyTiles,
    bool BodyInDungeon,
    bool KillerNearBody,
    int BluesNearBody,
    int MonstersNearBody,
    int GangMatesNear
);

/// <summary>What a blue band about to ride into Buccaneer's Den knows, read on the world thread.</summary>
public readonly record struct DenRaidFacts(int Riders, int RidersShortOfSupplies, int RedsInDen, DenRaidOutcome LastRaid);

/// <summary>
/// The ways back a red ghost has, read on the world thread: a free red to call over, the ankh
/// with the shortest trip, and a busy gang mate near to wait for. Only the ways the rules allow
/// are offered.
/// </summary>
public readonly record struct GhostWayFacts(
    bool CallOffered,
    bool CallIsGangMate,
    int CallTiles,
    bool AnkhOffered,
    int AnkhTripTiles,
    bool AnkhPassesGuards,
    int BluesAtAnkh,
    bool WaitOffered,
    int BusyMatesNear,
    bool KillerNear
);

/// <summary>The way back a red ghost takes.</summary>
public enum RedGhostWay
{
    /// <summary>Call the free red over to raise it where it stands.</summary>
    CallHelper,

    /// <summary>Walk to the ankh with the shortest trip.</summary>
    WalkToAnkh,

    /// <summary>Stay by the body a while for a busy gang mate to come and raise it.</summary>
    WaitForMate
}

/// <summary>
/// Jev's judgment at the big moments of red life, the owner's call of 2026-09-28: a red just
/// raised with its body close (walk back now, or recall home and re-arm), a blue band about to
/// raid the Den (go now, or hold), and a red ghost with more than one way back. Each is one
/// Choice among the options the hard rules already allow (guards, reach, what a skill can do),
/// asked once per moment from a few words of state. Every doubt keeps the rule pick
/// (<see cref="JevPick"/>, <see cref="JevWait"/>): the close walk, the raid, and the rule order
/// of the ghost's ways (call, then the ankh). Pure but for the asks.
/// </summary>
public static class RedMomentJev
{
    public const string WalkBackKey = "walk_back";
    public const string RecallHomeKey = "recall_home";
    public const string RaidKey = "raid";
    public const string HoldKey = "hold";
    public const string CallKey = "call";
    public const string AnkhKey = "ankh";
    public const string WaitKey = "wait";

    public const string WayBackInstructions =
        "This red was just raised near its body. Does it walk back to the body now in a death robe, " +
        "or recall home to Buccaneer's Den and re-arm first?";

    public const string DenRaidInstructions = "Does this band of lawful fighters raid Buccaneer's Den now?";

    public const string GhostWayInstructions = "This red is a ghost and wants to live again. Which way back does it take?";

    /// <summary>A body this close is a few steps off; farther, a short walk; past that, a long one.</summary>
    public const int FewStepsTiles = CombatStanceJev.FewStepsTiles;

    public const int ShortWalkTiles = 15;

    /// <summary>An ankh trip this long is a short walk; to <see cref="LongTripTiles"/> a long one; past it a crossing of the land.</summary>
    public const int ShortTripTiles = 60;

    public const int LongTripTiles = 250;

    /// <summary>This many reds at the Den or fewer are a few; more are many.</summary>
    public const int FewReds = 3;

    /// <summary>Killers, blue fighters and monsters this close to a body or an ankh wait there.</summary>
    public const int WatchTiles = GhostRules.AidSearchRange;

    /// <summary>A red ghost that chose to wait for a busy gang mate calls again this long, then takes the rule order.</summary>
    public static readonly TimeSpan MateWait = TimeSpan.FromMinutes(1);

    public static readonly IReadOnlyList<JevPickOption> WayBackOptions =
    [
        new(WalkBackKey, "walk to the body now and take the gear back", "the killer or blue fighters wait by the body"),
        new(RecallHomeKey, "recall home to the Den and re-arm from the bank", "the body lies close and clear")
    ];

    public static readonly IReadOnlyList<JevPickOption> DenRaidOptions =
    [
        new(RaidKey, "ride into the Den now and fight the reds there", "the band is small or short of supplies and the Den is full of reds"),
        new(HoldKey, "stay back and wait for a better time", "the band is strong and stocked and few reds are at the Den")
    ];

    private static readonly JevPickOption CallOption =
        new(CallKey, "call the free red over to raise you here", "your killer stands near");

    private static readonly JevPickOption AnkhOption =
        new(AnkhKey, "walk to the ankh and stand up there", "the way is long or passes a guarded town, or blue fighters wait there");

    private static readonly JevPickOption WaitOption =
        new(WaitKey, "wait by your body for a busy gang mate to come", "your killer stands near");

    // ----- A red just raised near its body -----

    /// <summary>
    /// A red's close walk to its body is put to Jev once per corpse run, and only when the rules
    /// allow both ways: the close, guard-free walk (<see cref="CorpseRunStep.CloseWalk"/>) and a
    /// recall home it can cast now. <paramref name="recallHomeOpen"/> reads the runebook and the
    /// means, so it is asked last.
    /// </summary>
    public static bool AsksWayBack(bool murderer, CorpseRunStep ruleStep, bool askedBefore, Func<bool> recallHomeOpen) =>
        murderer && ruleStep == CorpseRunStep.CloseWalk && !askedBefore && recallHomeOpen();

    public static JevDecision BuildWayBack(WayBackFacts facts) =>
        JevPick.Build(WayBackState(facts), WayBackInstructions, WayBackOptions);

    public static Dictionary<string, object> WayBackState(WayBackFacts facts) =>
        new()
        {
            ["you"] = $"a red just raised, in a death robe, {ArmedWord(facts.Armed)}",
            ["body"] = $"{BodyWord(facts.BodyTiles)}, {(facts.BodyInDungeon ? "in a dungeon" : "on open ground")}",
            ["at_body"] = AtBodyWords(facts.KillerNearBody, facts.BluesNearBody, facts.MonstersNearBody),
            ["gang"] = $"{CombatStanceJev.Counted(facts.GangMatesNear, "gang mate", "gang mates")} near"
        };

    /// <summary>Jev's recall home, or the rule's close walk for any other verdict.</summary>
    public static CorpseRunStep WayBackStep(JevPickVerdict verdict) =>
        verdict.Choice == RecallHomeKey ? CorpseRunStep.RecallHome : CorpseRunStep.CloseWalk;

    public static bool TryAskWayBack(SosariaCharacter red, WayBackFacts facts, Action<JevPickVerdict> onVerdict) =>
        JevPick.TryAsk(red, red?.LastKiller, BrainEventKind.Died, BuildWayBack(facts), WayBackOptions, onVerdict);

    // ----- A blue band about to ride into the Den -----

    public static JevDecision BuildDenRaid(DenRaidFacts facts) =>
        JevPick.Build(DenRaidState(facts), DenRaidInstructions, DenRaidOptions);

    public static Dictionary<string, object> DenRaidState(DenRaidFacts facts) =>
        new()
        {
            ["band"] = $"you and {CombatStanceJev.Counted(facts.Riders - 1, "friend", "friends")}",
            ["supplies"] = SuppliesWords(facts.RidersShortOfSupplies),
            ["den"] = $"{RedsWord(facts.RedsInDen)} seen at the Den",
            ["last_raid"] = LastRaidWords(facts.LastRaid)
        };

    /// <summary>The band rides unless Jev said to hold: the raid is the rule.</summary>
    public static bool RaidGoes(JevPickVerdict verdict) => verdict.Choice != HoldKey;

    public static bool TryAskDenRaid(SosariaCharacter leader, DenRaidFacts facts, Action<JevPickVerdict> onVerdict) =>
        JevPick.TryAsk(leader, null, BrainEventKind.Decide, BuildDenRaid(facts), DenRaidOptions, onVerdict);

    // ----- A red ghost with more than one way back -----

    /// <summary>The ways back the rules allow, in the rule order: call, then the ankh, then the wait.</summary>
    public static List<JevPickOption> GhostWayOptions(GhostWayFacts facts)
    {
        var options = new List<JevPickOption>();

        if (facts.CallOffered)
        {
            options.Add(CallOption);
        }

        if (facts.AnkhOffered)
        {
            options.Add(AnkhOption);
        }

        if (facts.WaitOffered)
        {
            options.Add(WaitOption);
        }

        return options;
    }

    /// <summary>
    /// A red ghost's way back may be put to Jev once per death, never while it is wanted under
    /// the guards (no red comes and no ankh there raises it). The ask goes out only with two or
    /// more ways open (<see cref="JevPick.TryAsk"/>).
    /// </summary>
    public static bool MayAskGhostWay(bool murderer, bool wantedUnderGuards, bool askedBefore) =>
        murderer && !wantedUnderGuards && !askedBefore;

    public static JevDecision BuildGhostWay(GhostWayFacts facts) =>
        JevPick.Build(GhostWayState(facts), GhostWayInstructions, GhostWayOptions(facts));

    public static Dictionary<string, object> GhostWayState(GhostWayFacts facts)
    {
        var state = new Dictionary<string, object>
        {
            ["you"] = facts.KillerNear ? "a red ghost, your killer stands near" : "a red ghost, your killer is gone"
        };

        if (facts.CallOffered)
        {
            state["helper"] = $"{(facts.CallIsGangMate ? "a gang mate" : "a fellow red")}, free, {TripWord(facts.CallTiles)}";
        }

        if (facts.AnkhOffered)
        {
            state["ankh"] =
                $"{TripWord(facts.AnkhTripTiles)}, {(facts.AnkhPassesGuards ? "passes a guarded town" : "clear of the guards")}, " +
                $"{CombatStanceJev.Counted(facts.BluesAtAnkh, "blue fighter", "blue fighters")} there";
        }

        if (facts.WaitOffered)
        {
            state["gang"] = $"{CombatStanceJev.Counted(facts.BusyMatesNear, "gang mate", "gang mates")} near, busy";
        }

        return state;
    }

    /// <summary>Jev's way when it is one the rules offered; else the rule order: call, then the ankh, then the wait.</summary>
    public static RedGhostWay GhostWay(JevPickVerdict verdict, GhostWayFacts facts) =>
        verdict.Choice switch
        {
            CallKey when facts.CallOffered => RedGhostWay.CallHelper,
            AnkhKey when facts.AnkhOffered => RedGhostWay.WalkToAnkh,
            WaitKey when facts.WaitOffered => RedGhostWay.WaitForMate,
            _ => facts.CallOffered ? RedGhostWay.CallHelper
                : facts.AnkhOffered ? RedGhostWay.WalkToAnkh
                : RedGhostWay.WaitForMate
        };

    public static bool TryAskGhostWay(SosariaCharacter ghost, GhostWayFacts facts, Action<JevPickVerdict> onVerdict) =>
        JevPick.TryAsk(ghost, ghost?.LastKiller, BrainEventKind.Died, BuildGhostWay(facts), GhostWayOptions(facts), onVerdict);

    // ----- Words -----

    public static string ArmedWord(bool armed) => armed ? "armed" : "unarmed";

    /// <summary>"a few steps away", "a short walk away", "a long walk away".</summary>
    public static string BodyWord(int tiles) =>
        tiles <= FewStepsTiles ? "a few steps away" : tiles <= ShortWalkTiles ? "a short walk away" : "a long walk away";

    /// <summary>"a short walk", "a long walk", "across the land".</summary>
    public static string TripWord(int tiles) =>
        tiles <= ShortTripTiles ? "a short walk" : tiles <= LongTripTiles ? "a long walk" : "across the land";

    /// <summary>"your killer, several blue fighters, one monster", or "nobody".</summary>
    public static string AtBodyWords(bool killer, int blues, int monsters)
    {
        var words = new List<string>();

        if (killer)
        {
            words.Add("your killer");
        }

        if (blues > 0)
        {
            words.Add(CombatStanceJev.Counted(blues, "blue fighter", "blue fighters"));
        }

        if (monsters > 0)
        {
            words.Add(CombatStanceJev.Counted(monsters, "monster", "monsters"));
        }

        return words.Count == 0 ? "nobody" : string.Join(", ", words);
    }

    public static string SuppliesWords(int shortOfSupplies) =>
        shortOfSupplies <= 0 ? "all stocked"
        : shortOfSupplies == CombatStanceJev.One ? "one short of supplies"
        : "several short of supplies";

    /// <summary>"no red", "a few reds", "many reds".</summary>
    public static string RedsWord(int reds) => reds <= 0 ? "no red" : reds <= FewReds ? "a few reds" : "many reds";

    public static string LastRaidWords(DenRaidOutcome outcome) =>
        outcome switch
        {
            DenRaidOutcome.CameHomeWhole => "the last raid came home whole",
            DenRaidOutcome.LostFighters => "the last raid lost fighters",
            _ => "no raid yet"
        };
}
