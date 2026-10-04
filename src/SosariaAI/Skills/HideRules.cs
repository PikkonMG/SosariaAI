using System;

namespace SosariaAI.Skills;

/// <summary>
/// T2A hiding: the engine's skill check, then Hidden. Fail if already hidden.
/// A hider pops back out after a while and hides again, the way a macroer blinked in and out.
/// </summary>
public static class HideRules
{
    public const int MinHiddenSeconds = 20;
    public const int MaxHiddenSeconds = 45;

    /// <summary>The engine's Hiding check failed.</summary>
    public const string NotHiddenReason = "could not hide";

    private const int InclusiveSpanPad = 1;

    /// <summary>How long a hider stays hidden before it reveals and hides again.</summary>
    public static TimeSpan HiddenFor(int roll) =>
        TimeSpan.FromSeconds(
            MinHiddenSeconds + Math.Abs(roll % (MaxHiddenSeconds - MinHiddenSeconds + InclusiveSpanPad))
        );
}
