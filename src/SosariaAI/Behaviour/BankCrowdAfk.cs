using System;

namespace SosariaAI.Behaviour;

/// <summary>An afk statue: stands where it stopped, and maybe says "afk" going and "back" leaving.</summary>
public sealed class BankCrowdAfk : BankCrowdAct
{
    public override bool Tick(DateTime now) => true;

    public override void End()
    {
        if (BankCrowdRules.Chance(Roll(), BankCrowdRules.AfkLinePercent))
        {
            Member.SpeakScripted(BankCrowdRules.BackLine);
        }
    }

    protected override bool OnStart()
    {
        Member.Motor.Stop();

        if (BankCrowdRules.Chance(Roll(), BankCrowdRules.AfkLinePercent))
        {
            Member.SpeakScripted(BankCrowdRules.AfkLine);
        }

        return true;
    }
}
