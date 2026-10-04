using System;
using Server;
using SosariaAI.Economy;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// A resist trainer: casts real weak curses on itself over and over. Mana and reagents go
/// for real; a dry trainer meditates, an empty reagent pouch is refilled from the bank box,
/// and a trainer with no reagents left leaves the crowd.
/// </summary>
public sealed class BankCrowdResist : BankCrowdAct
{
    private static readonly TimeSpan CastGap = TimeSpan.FromSeconds(BankCrowdRules.ResistCastGapSeconds);
    private static readonly TimeSpan MeditateGap = TimeSpan.FromSeconds(BankCrowdRules.MeditateSeconds);

    private DateTime _nextCast;
    private int _rotation;

    public override bool Tick(DateTime now)
    {
        if (SelfCurse.TryAimSelf(Member))
        {
            _nextCast = now + CastGap;
            return true;
        }

        if (Member.Spell != null || now < _nextCast)
        {
            return true;
        }

        if (Member.Mana < ResistRules.FirstCircleMana)
        {
            Server.Skills.UseSkill(Member, SkillName.Meditation);
            _nextCast = now + MeditateGap;
            return true;
        }

        if (SelfCurse.TryCast(Member, _rotation++))
        {
            return true;
        }

        _nextCast = now + CastGap;
        return SelfCurse.HasReagents(Member) || Restock();
    }

    public override void End() => GearEquip.EquipReadyWeapon(Member);

    protected override bool OnStart() =>
        SelfCurse.CanTrain(Member) && (SelfCurse.HasReagents(Member) || Restock());

    // Standing at the bank, the reserve is one "bank" away.
    private bool Restock()
    {
        if (!BankTeller.OpenBox(Member))
        {
            return false;
        }

        SupplyCheck.TakeFromBank(Member, new SupplyNeed(SupplyKind.Reagents, 0, SupplyRules.ReagentTarget));
        return SelfCurse.HasReagents(Member);
    }
}
