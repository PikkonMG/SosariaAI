using System;
using SosariaAI.Common;

namespace SosariaAI.Combat;

/// <summary>
/// Person fights end at the guard line. War rallies and outlaw hunts refuse to
/// start under the guards, but a chase crosses the line: the fighter drops the
/// foe instead of brawling at the bank.
/// </summary>
public static class GuardLineRules
{
    /// <summary>
    /// A fighter who just backed off does not start the same fight again a few
    /// seconds later. Without the memory, two enemies at the boundary rally and
    /// break off every few seconds forever.
    /// </summary>
    public static readonly TimeSpan StandDownGrace = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A foe this close to guarded ground stands at the guard line: its first step takes the
    /// fight under the guards, where it is stood down at once. Dame Alys drew on the people at
    /// the Trinsic gate and broke off at the line 50 times in two hours, twice a second at worst.
    /// </summary>
    public const int LineMarginTiles = 6;

    /// <summary>The eight points round a spot at <see cref="LineMarginTiles"/>, as offsets: a guarded one puts the spot at the line.</summary>
    public static readonly (int X, int Y)[] LineRing = Ring(LineMarginTiles);

    /// <summary>The eight points round a spot at <paramref name="margin"/> tiles, as offsets.</summary>
    public static (int X, int Y)[] Ring(int margin) =>
        [(-margin, -margin), (0, -margin), (margin, -margin), (-margin, 0), (margin, 0), (-margin, margin), (0, margin), (margin, margin)];

    public static bool ShouldStandDown(bool selfUnderGuards, bool foeUnderGuards) =>
        selfUnderGuards || foeUnderGuards;

    public static bool InStandDownGrace(DateTime last, DateTime now) =>
        !TimeRules.Rested(last, now, StandDownGrace);

    /// <summary>
    /// The stand-down is said once in the grace. Osanna said "guards... later" to three foes in
    /// 21 seconds at the Yew moongate.
    /// </summary>
    public static bool SpeaksAtLine(DateTime lastLineStop, DateTime now) => !InStandDownGrace(lastLineStop, now);

    /// <summary>
    /// True when a red breaks off its fight and runs: it stands under the guards, whoever the
    /// foe is, a real player as much as a character, and it is not running already. Before AOS
    /// the guards come for a red the moment it steps in near a townsperson, and any shout for
    /// them within fourteen tiles kills it (GuardedRegion), so the fight there is lost either way.
    /// </summary>
    public static bool RedBreaksOff(bool murderer, bool underGuards, bool fighting, bool running) =>
        murderer && underGuards && fighting && !running;
}
