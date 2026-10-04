using System;
using Server;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>A hider: hides with the real skill, stays hidden a while, reveals, and hides again.</summary>
public sealed class BankCrowdHider : BankCrowdAct
{
    private DateTime _revealAt;

    public override bool Tick(DateTime now)
    {
        if (!Member.Hidden)
        {
            if (Server.Skills.UseSkill(Member, SkillName.Hiding) && Member.Hidden)
            {
                _revealAt = now + HideRules.HiddenFor(Roll());
            }

            return true;
        }

        if (now >= _revealAt)
        {
            Member.RevealingAction();
        }

        return true;
    }

    public override void End() => Unhide();

    protected override bool OnStart()
    {
        Member.Motor.Stop();
        return true;
    }
}
