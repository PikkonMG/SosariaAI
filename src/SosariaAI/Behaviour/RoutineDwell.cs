using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// How long the routine holds after a skill ends before the next choice. Practice skills
/// once ended on the tick they began and the routine froze the character for twenty
/// seconds, which read as standing about. They now run as practice sessions that last
/// minutes. A step that still ends at once, such as a recall, holds for a single think,
/// so the next choice waits for the next pulse instead of the same one.
/// </summary>
public static class RoutineDwell
{
    public const int InstantTicks = 1;
    public const int InstantDoneHoldTicks = 1;

    public static int HoldTicks(SkillStatus status, int ticksRun) =>
        status == SkillStatus.Done && ticksRun <= InstantTicks ? InstantDoneHoldTicks : 0;
}
