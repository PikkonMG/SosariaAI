using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class DispositionTests
{
    [Fact]
    public void Parse_PkSlot_IsOutlaw() =>
        Assert.Equal(DispositionKind.Outlaw, DispositionRules.Parse("lawful", isPk: true));

    [Fact]
    public void Parse_NamedKinds()
    {
        Assert.Equal(DispositionKind.Lawful, DispositionRules.Parse(DispositionRules.LawfulName, isPk: false));
        Assert.Equal(DispositionKind.Outlaw, DispositionRules.Parse(DispositionRules.OutlawName, isPk: false));
        Assert.Equal(DispositionKind.Neutral, DispositionRules.Parse(DispositionRules.NeutralName, isPk: false));
        Assert.Equal(DispositionKind.Neutral, DispositionRules.Parse(null, isPk: false));
    }

    [Fact]
    public void Courage_UsesPersonaDrives()
    {
        var drives = new PersonaDrives(greed: 0.8, caution: 0.2, valor: 0.7, isCustom: true);
        Assert.Equal(0.7, DispositionRules.CourageOf(drives));
        Assert.Equal(DispositionRules.DefaultCourage, DispositionRules.CourageOf(null));
    }
}
