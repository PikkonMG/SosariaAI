using System;
using Server;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// A stealth trainer: hides, uses Stealth, and creeps a tight ring around its place one slow
/// step at a time. When the quiet steps run out the engine reveals it, and it starts over.
/// </summary>
public sealed class BankCrowdStealth : BankCrowdAct
{
    private DateTime _nextStep;
    private int _ring;

    public override bool Tick(DateTime now)
    {
        if (Member.Mounted)
        {
            return false;
        }

        if (!Member.Hidden)
        {
            Server.Skills.UseSkill(Member, SkillName.Hiding);
            return true;
        }

        if (Member.AllowedStealthSteps <= 0)
        {
            Server.Skills.UseSkill(Member, SkillName.Stealth);
            return true;
        }

        if (now >= _nextStep)
        {
            _nextStep = now + StealthRules.StepGap;
            StealthCreep.Step(Member, Seat.Spot, ref _ring);
        }

        return true;
    }

    public override void End() => Unhide();

    protected override bool OnStart() => !Member.Mounted;
}
