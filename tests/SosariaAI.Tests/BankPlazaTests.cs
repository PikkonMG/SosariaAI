using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class BankPlazaTests
{
    private static readonly Point3D Bank = CharactersFile.DefaultBankSpot;
    private static readonly Point3D RiverPile = new(1415, 1700, 0);
    private static readonly Point3D CastleGate = new(1365, 1765, 0);

    [Fact]
    public void Contains_BankAndRiverPile_AreOnThePlaza()
    {
        Assert.True(BankPlaza.Contains(Bank, Bank));
        Assert.True(BankPlaza.Contains(RiverPile, Bank));
        Assert.False(BankPlaza.Contains(CastleGate, Bank));
        Assert.False(BankPlaza.Contains(Bank, Point3D.Zero));
    }

    [Fact]
    public void MayStartPersonFight_NotOnThePlaza()
    {
        Assert.False(BankPlaza.MayStartPersonFight(onPlaza: true));
        Assert.True(BankPlaza.MayStartPersonFight(onPlaza: false));
    }
}
