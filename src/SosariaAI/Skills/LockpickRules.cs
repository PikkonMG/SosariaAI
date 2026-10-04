using System;
using Server;
using Server.Items;
using SosariaAI.Economy;

namespace SosariaAI.Skills;

/// <summary>
/// T2A lockpicking: a locked chest one tile off, a lockpick from the pack, the engine's
/// three-second pick and its Lockpicking check. The engine's lockpick opens no door, and a
/// chest inside someone's house is not a thief's practice.
/// </summary>
public static class LockpickRules
{
    public const int ReachTiles = 1;
    /// <summary>A lock picker starts with the handful it restocks to (<see cref="SupplyRules.LockpickTarget"/>).</summary>
    public const int StartingAmount = SupplyRules.LockpickTarget;

    /// <summary>The picker reads the lock a moment after the engine's three-second pick resolved.</summary>
    public static readonly TimeSpan PickWait = TimeSpan.FromSeconds(4);

    public static bool IsPickable(bool locked, int lockLevel) =>
        locked && lockLevel is not ILockpickable.CannotPick and not ILockpickable.MagicLock;

    /// <summary>A lock worth trying: locked, pickable by normal means, and not inside a house.</summary>
    public static bool IsTarget(bool locked, int lockLevel, bool insideHouse) =>
        !insideHouse && IsPickable(locked, lockLevel);

    public static bool SkillMeetsRequired(double lockpicking, int requiredSkill) =>
        lockpicking >= requiredSkill;
}
