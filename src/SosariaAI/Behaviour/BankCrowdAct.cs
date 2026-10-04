using System;
using Server;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>
/// What a crowd member does while it holds its place, one class per role. Start runs on
/// arrival; Tick runs every think and answers false when the role cannot go on; End runs
/// when the member leaves.
/// </summary>
public abstract class BankCrowdAct
{
    protected SosariaCharacter Member { get; private set; }

    protected BankCrowdSeat Seat { get; private set; }

    public static BankCrowdAct For(BankCrowdRole role) =>
        role switch
        {
            BankCrowdRole.Hawker => new BankCrowdHawker(),
            BankCrowdRole.Afk => new BankCrowdAfk(),
            BankCrowdRole.ResistTrainer => new BankCrowdResist(),
            BankCrowdRole.Hider => new BankCrowdHider(),
            BankCrowdRole.StealthTrainer => new BankCrowdStealth(),
            BankCrowdRole.Beggar => new BankCrowdStreet(TalkCategory.StreetBeg, TalkCategory.StreetBegGiveUp),
            BankCrowdRole.Newbie => new BankCrowdStreet(TalkCategory.StreetNewbie, TalkCategory.StreetNewbieGiveUp),
            _ => new BankCrowdRegular()
        };

    public bool Start(SosariaCharacter member, BankCrowdSeat seat)
    {
        Member = member;
        Seat = seat;
        return OnStart();
    }

    public abstract bool Tick(DateTime now);

    public virtual void End()
    {
    }

    protected abstract bool OnStart();

    protected static int Roll() => Utility.Random(int.MaxValue);

    /// <summary>Turns to the nearest visible person in talking range: you talk to someone, not to a wall.</summary>
    protected void FaceNearest() => LoiterPace.FaceNearest(Member);

    /// <summary>A hidden member steps out of hiding before it leaves the crowd.</summary>
    protected void Unhide()
    {
        if (Member is { Hidden: true, Deleted: false })
        {
            Member.RevealingAction();
        }
    }
}
