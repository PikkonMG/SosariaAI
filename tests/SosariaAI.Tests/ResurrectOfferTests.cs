using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// trollslayer killed Lysa, said "got u, one sec" to a ghost beside her body a second later
/// and failed at once; Nightshade knelt to raise while Casimir attacked it. A raise never
/// comes from a hand in the kill, never under a foe's blows and never with the killer at
/// the body.
/// </summary>
public class ResurrectOfferTests
{
    private const int Blow = 12;
    private const int Beside = 1;
    private const int FarOff = ResurrectAid.FoeRange + 1;
    private const int KillerFarOff = GhostRules.AidSearchRange + 1;
    private static uint _nextSerial = 0x8A01;
    private static readonly Point3D Moonglow = new(4450, 1150, 0);

    static ResurrectOfferTests() => Timer.Init(0);

    public ResurrectOfferTests() => TestMap.EnsureInternal();

    [Fact]
    public void TookPartInKill_TheKillerAndAnyoneWhoHurtTheFallen()
    {
        var ghost = Person(Moonglow);
        var killer = Person(Moonglow);
        var accomplice = Person(Moonglow);
        var stranger = Person(Moonglow);
        ghost.LastKiller = killer;
        ghost.RegisterDamage(Blow, accomplice);

        Assert.True(ResurrectOffer.TookPartInKill(killer, ghost));
        Assert.True(ResurrectOffer.TookPartInKill(accomplice, ghost));
        Assert.False(ResurrectOffer.TookPartInKill(stranger, ghost));
        Assert.False(ResurrectOffer.WillAid(killer, ghost, asked: true));
        Assert.False(ResurrectOffer.WillAid(accomplice, ghost, asked: true));
    }

    [Fact]
    public void KillerNear_OnlyALivingKillerWithinAPleasReach()
    {
        var ghost = Person(Moonglow);
        var killer = Person(Offset(Moonglow, Beside));
        ghost.LastKiller = killer;

        Assert.True(ResurrectOffer.KillerNear(ghost));

        killer.Location = Offset(Moonglow, KillerFarOff);

        Assert.False(ResurrectOffer.KillerNear(ghost));
        Assert.False(ResurrectOffer.KillerNear(Person(Moonglow)));
    }

    [Fact]
    public void FoeOn_OnlyAFoeNearThatAimsAtTheHelper()
    {
        var helper = Person(Moonglow);
        var foe = Person(Offset(Moonglow, Beside));
        var bystander = Person(Offset(Moonglow, Beside));
        bystander.Combatant = foe;

        Assert.Null(ResurrectOffer.FoeOn(helper));

        helper.Aggressors.Add(AggressorInfo.Create(foe, helper, false));
        foe.Combatant = helper;

        Assert.Equal(foe, ResurrectOffer.FoeOn(helper));
        Assert.Equal(ResurrectAid.AttackedWhy(foe.Name), ResurrectOffer.Hindrance(helper, Person(Moonglow)));

        foe.Location = Offset(Moonglow, FarOff);

        Assert.Null(ResurrectOffer.FoeOn(helper));
    }

    [Fact]
    public void CallRedHelper_OnlyARedGhostOnTheMapCalls()
    {
        var livingRed = Person(Moonglow);
        livingRed.Kills = PkRules.MurdersToRed;

        Assert.Null(ResurrectOffer.CallRedHelper(livingRed));
        Assert.Null(ResurrectOffer.CallRedHelper(null));
        Assert.Null(ResurrectOffer.FindRedHelper(livingRed, out var gangMate));
        Assert.False(gangMate);
    }

    private static Point3D Offset(Point3D from, int tiles) => new(from.X + tiles, from.Y, from.Z);

    private static SosariaCharacter Person(Point3D at)
    {
        var person = new SosariaCharacter((Serial)_nextSerial++);
        person.DefaultMobileInit();
        person.Name = $"person {person.Serial.Value}";
        person.Location = at;
        return person;
    }
}
