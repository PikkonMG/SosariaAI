using System;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// A regular: banks at the counter on arrival, then talks with whoever stands near, and
/// between chats now and then says "bank" to look into its box.
/// </summary>
public sealed class BankCrowdRegular : BankCrowdAct
{
    private DateTime _nextChat;
    private int _turn;

    public override bool Tick(DateTime now)
    {
        if (now < _nextChat)
        {
            return true;
        }

        _nextChat = now + BankCrowdRules.ChatGap(Roll());
        FaceNearest();

        if (!Meeting.TryChatNearby(Member, null, _turn++) &&
            BankCrowdRules.Chance(Roll(), BankCrowdRules.RegularBoxCheckPercent))
        {
            BankTeller.OpenBox(Member);
        }

        return true;
    }

    protected override bool OnStart()
    {
        BankTeller.SettleWalkingMoney(Member);
        _nextChat = Server.Core.Now + BankCrowdRules.ChatGap(Roll());
        return true;
    }
}
