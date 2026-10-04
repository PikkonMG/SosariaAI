using Server.Items;
using SosariaAI.Configuration;

namespace SosariaAI.Skills;

/// <summary>
/// T2A remove trap: disarm a nearby chest. Classic thief dungeon skill.
/// </summary>
public static class RemoveTrapRules
{
    public const string Kind = SkillKinds.RemoveTrap;
    public const int ReachTiles = 2;
    public const double TrapPowerWindow = 30;
    public const int ClearedTrapPower = 0;
    public const int ClearedTrapLevel = 0;

    public static bool IsTrapped(TrapType trapType) => trapType != TrapType.None;

    public static (double Min, double Max) SkillWindow(int trapPower) =>
        (trapPower, trapPower + TrapPowerWindow);

    public static void Disarm(TrappableContainer container)
    {
        if (container == null)
        {
            return;
        }

        container.TrapPower = ClearedTrapPower;
        container.TrapLevel = ClearedTrapLevel;
        container.TrapType = TrapType.None;
    }
}
