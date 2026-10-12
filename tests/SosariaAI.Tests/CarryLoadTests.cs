using Server;
using Server.Items;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A buy stops at the body's carry limit, and an overloaded crafter sheds its craft stock.
/// A tailor bought 47 hides of 5 stones each, stood at 317 of 253 stones, and could not walk
/// for four hours: only harvest was shed, and the hides were not harvest.
/// </summary>
public class CarryLoadTests
{
    // A T2A body of 50 strength carries 40 + 3.5 * 50 = 215 stones (PlayerMobile.MaxWeight),
    // its own 14 stones (Mobile.BodyWeight) among them: 201 stones of room, 40 hides.
    private const int Strength = 50;
    private const int HidesAsked = 47;
    private const int HidesThatFit = 40;
    private const int IngotsAsked = 100;

    static CarryLoadTests() => Timer.Init(0);

    public CarryLoadTests() => TestMap.EnsureInternal();

    [Fact]
    public void UnitsItCarries_StopsAtTheBodyLimit()
    {
        var person = Person((Serial)0x7D40);

        Assert.Equal(HidesThatFit, CarryLoad.UnitsItCarries(person, new Hides((Serial)0x7D42) { Amount = 1 }, HidesAsked));
        Assert.Equal(IngotsAsked, CarryLoad.UnitsItCarries(person, new IronIngot((Serial)0x7D43) { Amount = 1 }, IngotsAsked));
    }

    [Fact]
    public void IsHaul_HarvestAndCraftStock_NotGearOrSupplies()
    {
        Assert.True(CarryLoad.IsHaul(new Hides((Serial)0x7D44)));
        Assert.True(CarryLoad.IsHaul(new Leather((Serial)0x7D45)));
        Assert.True(CarryLoad.IsHaul(new Board((Serial)0x7D46)));
        Assert.True(CarryLoad.IsHaul(new Log((Serial)0x7D47)));
        Assert.True(CarryLoad.IsHaul(new IronIngot((Serial)0x7D48)));
        Assert.False(CarryLoad.IsHaul(new Bandage((Serial)0x7D49)));
        Assert.False(CarryLoad.IsHaul(new BlackPearl((Serial)0x7D4A)));
        Assert.False(CarryLoad.IsHaul(new SewingKit((Serial)0x7D4B)));
        Assert.False(CarryLoad.IsHaul(new LeatherCap((Serial)0x7D4C)));
    }

    private static SosariaCharacter Person(Serial serial)
    {
        var person = new SosariaCharacter(serial);
        person.DefaultMobileInit();
        person.RawStr = Strength;
        return person;
    }
}
