using System;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

public enum GhostPhase
{
    Haunt,
    SeekAid,
    AskHelper,
    ExitDungeon,
    WalkToShrine,
    CorpseRun,
    Fallback
}

/// <summary>
/// Player-like death flow. A character dies as a PlayerMobile, so the engine makes the
/// ghost and SosariaCharacter.IsGhost is simply !Alive. The ghost wails over its body a
/// moment, then asks a living person in sight for a res, climbs out of a dungeon, or
/// walks to an ankh or a healer. Once alive it runs back to its corpse (see
/// <see cref="CorpseRunRules"/>). Standing up at home with no raise is the last resort,
/// not a timer: 246 of 410 raises in one hour were that.
/// </summary>
public static class GhostRules
{
    /// <summary>A ghost stands up on its own only after this long dead.</summary>
    public static readonly TimeSpan FallbackAfter = TimeSpan.FromMinutes(30);

    /// <summary>And only when its way back made no headway for this long.</summary>
    public static readonly TimeSpan StuckFor = TimeSpan.FromMinutes(20);

    /// <summary>The backstop for a ghost whose way back never runs at all.</summary>
    public static readonly TimeSpan BackstopAfter = TimeSpan.FromHours(2);

    /// <summary>
    /// A ghost whose look finds no healer, shrine or friend it can get to stands up on its own
    /// once its way back made no headway for this long, not after <see cref="FallbackAfter"/>:
    /// blues on the Deceit island asked for the same six healers for half an hour, and none had
    /// a road.
    /// </summary>
    public static readonly TimeSpan NoWayFallbackAfter = TimeSpan.FromMinutes(3);

    public const string StuckReason = "no way to a healer, a shrine or a friend";
    public const string NoWayReason = "no healer, shrine or friend it can reach from here";
    public const string BackstopReason = "its way back never ran";

    public static readonly TimeSpan HauntMin = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan HauntMax = TimeSpan.FromSeconds(20);

    /// <summary>
    /// A gray ghost stays by its body at most this long for its two-minute flag to lapse, then
    /// the town healer takes it. A thief cut down at the bank walked off to a far shrine instead,
    /// and its body rotted before it came back.
    /// </summary>
    public static readonly TimeSpan GrayWaitLimit = TimeSpan.FromMinutes(3);

    /// <summary>
    /// A way back that found no road is looked for again after this wait, doubled for each
    /// miss in a row up to <see cref="SeekRetryMax"/>. A ghost on an island with no road asked
    /// for a route every ten seconds for twenty minutes.
    /// </summary>
    public static readonly TimeSpan SeekRetryFirst = TimeSpan.FromSeconds(10);

    public static readonly TimeSpan SeekRetryMax = TimeSpan.FromMinutes(2);

    /// <summary>A helper asked once is asked again after this wait, then left after <see cref="AskWait"/>.</summary>
    public static readonly TimeSpan AskRepeat = TimeSpan.FromSeconds(12);

    public static readonly TimeSpan AskWait = TimeSpan.FromSeconds(30);

    /// <summary>A person who let the ghost down is not asked again for this long.</summary>
    public static readonly TimeSpan HelperRefuseFor = TimeSpan.FromMinutes(3);

    /// <summary>An ankh or healer that did not raise the ghost is skipped for this long.</summary>
    public static readonly TimeSpan SiteRefuseFor = TimeSpan.FromMinutes(5);

    /// <summary>On a walk the ghost looks around for a living helper this often.</summary>
    public static readonly TimeSpan HelperScanGap = TimeSpan.FromSeconds(6);

    public const int AidSearchRange = 12;
    public const int AskRange = 1;
    public const int MaxAsksPerHelper = 2;
    public const int HealerRange = 4;
    public const int AnkhRange = 2;

    /// <summary>How far round a catalog shrine spot its ankh is looked for.</summary>
    public const int AnkhSearchTiles = 8;

    /// <summary>A ghost walks onto its raise tile itself: a tile beside it can be a pad.</summary>
    public const int ShrineStandTiles = 0;

    public const int HauntDrift = 3;
    public const int ThanksRange = 3;
    public const int ResurrectSound = 0x214;
    public const int ResurrectEffect = 0x376A;
    public const int ResurrectEffectDuration = 16;

    /// <summary>
    /// Long dead and stuck: no step taken and nobody on the way to raise it for
    /// <see cref="StuckFor"/>. A ghost that walks, or waits on a helper, keeps walking.
    /// </summary>
    public static bool ShouldFallback(DateTime now, DateTime ghostSince, DateTime lastProgressAt) =>
        TimeRules.Passed(ghostSince, now, FallbackAfter) && TimeRules.Rested(lastProgressAt, now, StuckFor);

    /// <summary>
    /// A look found no healer, shrine or friend the ghost can get to, and its way back made no
    /// headway (a step taken, or a helper on the way) for <see cref="NoWayFallbackAfter"/>.
    /// </summary>
    public static bool ShouldFallbackNoWay(DateTime now, DateTime lastProgressAt) =>
        TimeRules.Rested(lastProgressAt, now, NoWayFallbackAfter);

    /// <summary>
    /// When the backstop fires: two hours after death, never sooner than the stuck wait from
    /// now. Ghosts kept over a restart all stood up in the boot second before.
    /// </summary>
    public static TimeSpan BackstopDelay(DateTime now, DateTime ghostSince)
    {
        var left = ghostSince == default ? BackstopAfter : BackstopAfter - (now - ghostSince);
        return left < StuckFor ? StuckFor : left;
    }

    /// <summary>
    /// The ghost stays by its body while it wails, and a gray one (not a red, whom every healer
    /// turns away) until its flag lapses or <see cref="GrayWaitLimit"/> after its death.
    /// </summary>
    public static bool KeepsHaunting(DateTime now, DateTime hauntUntil, bool criminal, bool murderer, DateTime ghostSince) =>
        now < hauntUntil || criminal && !murderer && ghostSince != default && now - ghostSince < GrayWaitLimit;

    /// <summary>The wait before the next look for a way back, after <paramref name="misses"/> in a row.</summary>
    public static TimeSpan SeekRetryDelay(int misses)
    {
        var wait = SeekRetryFirst;

        for (var i = 1; i < misses && wait < SeekRetryMax; i++)
        {
            wait += wait;
        }

        return wait < SeekRetryMax ? wait : SeekRetryMax;
    }

    /// <summary>How long the ghost wails over its body, spread by its serial.</summary>
    public static TimeSpan HauntFor(long serial)
    {
        var spread = (long)(HauntMax - HauntMin).TotalSeconds;
        return HauntMin + TimeSpan.FromSeconds(Math.Abs(serial % spread));
    }

    /// <summary>
    /// A living helper in sight comes first. Underground the way out is the way back
    /// to life; overland the ghost walks to an ankh or a healer.
    /// </summary>
    public static GhostPhase NextRoute(bool helperInSight, bool underground)
    {
        if (helperInSight)
        {
            return GhostPhase.AskHelper;
        }

        return underground ? GhostPhase.ExitDungeon : GhostPhase.WalkToShrine;
    }

    public static GhostPhase AfterRaised() => GhostPhase.CorpseRun;

    /// <summary>The first plea at once, one more after <see cref="AskRepeat"/>.</summary>
    public static bool AskIsDue(DateTime now, DateTime askedAt, int asks) =>
        asks == 0 || asks < MaxAsksPerHelper && now - askedAt >= AskRepeat;

    /// <summary>The helper was asked and nothing came of it in time.</summary>
    public static bool AskExpired(DateTime now, DateTime askedAt) =>
        TimeRules.Passed(askedAt, now, AskWait);

    /// <summary>True while a refusal mark still holds.</summary>
    public static bool StillRefused(DateTime now, DateTime refusedUntil) => now < refusedUntil;

    /// <summary>
    /// Do not ask for a raise while the killer is still in reach. The graveyard filled
    /// with one person's corpses: raise, loot, die, again.
    /// </summary>
    public static bool MayRaiseInPlace(bool killerNearby) => !killerNearby;
}
