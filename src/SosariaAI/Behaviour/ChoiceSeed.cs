using System;

namespace SosariaAI.Behaviour;

/// <summary>
/// The seed behind a person's choices. It holds for one job phase: a stretch of 30 to 180
/// minutes whose length and start are the person's own. The job dice, the plan pick and
/// the near-tie jitter all read it, so a person keeps one purpose for the phase, and no two
/// people change purpose on the same minute.
/// </summary>
public static class ChoiceSeed
{
    public const int MinPhaseMinutes = 30;
    public const int MaxPhaseMinutes = 180;

    /// <summary>A restless person's shortest phase and a homebody's longest, after the trait scale.</summary>
    public const int MinScaledPhaseMinutes = 10;
    public const int MaxScaledPhaseMinutes = 360;
    public const double NeutralPhaseScale = 1.0;

    private const uint PhaseLengthSalt = 0x5BD1E995;
    private const uint PhaseOffsetSalt = 0x27D4EB2F;
    private const uint PhaseIndexPrime = 0x9E3779B1;
    private const int InclusiveSpanPad = 1;
    private const int MixShift1 = 16;
    private const int MixShift2 = 15;
    private const uint MixPrime1 = 0x7feb352d;
    private const uint MixPrime2 = 0x846ca68b;
    private const double UnitScale = 1.0 / (1L << 32);

    private static readonly long EpochTicks = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    public static int For(uint serial, DateTime now, double phaseScale = NeutralPhaseScale) =>
        unchecked((int)Mix(serial ^ (uint)PhaseIndex(serial, now, phaseScale) * PhaseIndexPrime));

    /// <summary>
    /// How long one job phase lasts for this person. Traits scale it: a restless person
    /// moves on sooner, a homebody stays with one purpose far longer.
    /// </summary>
    public static int PhaseMinutes(uint serial, double phaseScale = NeutralPhaseScale)
    {
        var baseMinutes = MinPhaseMinutes +
                          (int)(Mix(serial ^ PhaseLengthSalt) % (uint)(MaxPhaseMinutes - MinPhaseMinutes + InclusiveSpanPad));
        return Math.Clamp((int)Math.Round(baseMinutes * phaseScale), MinScaledPhaseMinutes, MaxScaledPhaseMinutes);
    }

    /// <summary>
    /// The number of the phase this person is in. Each person's phases start on their own
    /// minute, so a crowd never ends its jobs together.
    /// </summary>
    public static long PhaseIndex(uint serial, DateTime now, double phaseScale = NeutralPhaseScale)
    {
        var length = PhaseMinutes(serial, phaseScale);
        var offset = Mix(serial ^ PhaseOffsetSalt) % (uint)length;
        var minutes = (now.Ticks - EpochTicks) / TimeSpan.TicksPerMinute;
        return (minutes + offset) / length;
    }

    /// <summary>A value in [0, 1) from a seed and a salt. Pure, and spread across the range.</summary>
    public static double Unit(int seed, int salt) =>
        Mix(unchecked((uint)seed ^ (uint)salt * PhaseIndexPrime)) * UnitScale;

    public static uint Mix(uint seed)
    {
        unchecked
        {
            seed ^= seed >> MixShift1;
            seed *= MixPrime1;
            seed ^= seed >> MixShift2;
            seed *= MixPrime2;
            seed ^= seed >> MixShift1;
            return seed;
        }
    }
}
