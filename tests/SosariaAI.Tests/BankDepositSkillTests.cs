using Server;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class BankDepositSkillTests
{
    /// <summary>The Buccaneer's Den teller in the live catalog.</summary>
    private static readonly Point3D DenTeller = new(2731, 2192, 0);

    [Fact]
    public void AtBankSpot_InsideTheBankArea_Counts()
    {
        var bank = CharactersFile.DefaultBankSpot;

        // Standing on the plaza or across the counter row still counts as at the bank.
        Assert.True(BankDepositSkill.AtBankSpot(bank, bank));
        Assert.True(BankDepositSkill.AtBankSpot(new Point3D(1418, 1695, 0), bank));

        // A failed walk leg far from the target still fails.
        Assert.False(BankDepositSkill.AtBankSpot(new Point3D(2728, 893, 0), bank));
    }

    [Fact]
    public void BankFor_AMurdererBoundForATownBank_WalksToTheDenTeller()
    {
        var town = CharactersFile.DefaultBankSpot;
        var den = new Destination { Name = "Den Bank", Kind = "Bank", X = DenTeller.X, Y = DenTeller.Y };

        Assert.Equal(den.Arrival, BankDepositSkill.BankFor(town, murderer: true, den));
        Assert.Equal(town, BankDepositSkill.BankFor(town, murderer: false, den));
        Assert.Equal(PkRules.BucsDenHaven, BankDepositSkill.BankFor(PkRules.BucsDenHaven, murderer: true, den));
        Assert.Equal(town, BankDepositSkill.BankFor(town, murderer: true, allowedBank: null));
    }
}
