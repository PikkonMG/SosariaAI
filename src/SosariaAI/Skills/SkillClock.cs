using System;

namespace SosariaAI.Skills;

public static class SkillClock
{
    /// <summary>Moves a clock mark forward by a held duration. An unset mark stays unset.</summary>
    public static DateTime Shift(DateTime mark, TimeSpan held) =>
        mark == default ? mark : mark + held;
}
