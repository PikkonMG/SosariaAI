using System;
using Server;
using Server.Items;
using Server.Multis;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Picks a locked chest in reach with the engine's own lockpick: its target, its three-second
/// pick, its skill check and the pick that breaks on a miss. The step once unlocked any locked
/// door in reach by decree, a house door too; the engine's lockpick refuses every door. The
/// thief stands still at the lock for the pick: a step off it ends the engine's pick unheard.
/// </summary>
public sealed class LockpickSkill : Skill
{
    private const string NoLockpickReason = "no lockpicks";
    private const string NoChestReason = "no locked chest in reach";
    private const string TooHardReason = "the lock is beyond its skill";
    private const string HeldReason = "the lock held";

    private SosariaCharacter _thief;
    private ILockpickable _chest;
    private DateTime _pickedAt;

    public override string Name => SkillKinds.Lockpick;

    /// <summary>
    /// A lockpick in the pack and a locked chest in reach it has the skill for. A thief with
    /// neither failed "no lockpicks" 228 times in one night at the bank. A thief out of picks
    /// buys them at the tinker or the provisioner as a supply (<see cref="SupplyRules.LockpickTarget"/>);
    /// with no seller in reach it does not pick the job.
    /// </summary>
    public static bool HasWork(SosariaCharacter thief) =>
        People.InWorld(thief) && thief.Backpack?.FindItemByType<Lockpick>() != null &&
        FindLocked(thief) is { } chest &&
        LockpickRules.SkillMeetsRequired(thief.Skills.Lockpicking.Value, chest.RequiredSkill);

    public override bool Begin(SosariaCharacter character)
    {
        _thief = character;
        _chest = null;

        if (!People.InWorld(character))
        {
            return CannotStart(NotInWorldReason);
        }

        var lockpick = character.Backpack?.FindItemByType<Lockpick>();

        if (lockpick == null)
        {
            return CannotStart(NoLockpickReason);
        }

        _chest = FindLocked(character);

        if (_chest == null)
        {
            return CannotStart(NoChestReason);
        }

        if (!LockpickRules.SkillMeetsRequired(character.Skills.Lockpicking.Value, _chest.RequiredSkill))
        {
            return CannotStart(TooHardReason);
        }

        character.Motor.Stop();
        lockpick.OnDoubleClick(character);
        character.Target?.Invoke(character, _chest);
        _pickedAt = Core.Now;
        return true;
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_thief) || _thief.Deleted || _chest == null)
        {
            return Fail(NotInWorldReason);
        }

        if (!_chest.Locked)
        {
            return SkillStatus.Done;
        }

        if (Core.Now - _pickedAt >= LockpickRules.PickWait)
        {
            return Fail(HeldReason);
        }

        _thief.Motor.ClearMoveIntent();
        return SkillStatus.Running;
    }

    public override void Abort()
    {
        _thief = null;
        _chest = null;
    }

    public override void Resume(TimeSpan held) => _pickedAt = SkillClock.Shift(_pickedAt, held);

    private static ILockpickable FindLocked(SosariaCharacter thief)
    {
        foreach (var item in thief.GetItemsInRange(LockpickRules.ReachTiles))
        {
            if (item is ILockpickable pickable && !item.Deleted &&
                LockpickRules.IsTarget(pickable.Locked, pickable.LockLevel, BaseHouse.FindHouseAt(item) != null))
            {
                return pickable;
            }
        }

        return null;
    }
}
