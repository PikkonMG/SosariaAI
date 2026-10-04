using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// Outside a raid, a blue in Buccaneer's Den starts no fight unless an outlaw is hurting its
/// friend. Only the lawful draw asked it: the gray watch, Order against Chaos, a guild war,
/// the draft, the combat brain's own foes and a follower's take of its leader's foe drew on
/// anyone there. Each of them now asks <see cref="WorldPlay.MayStartFightInDen"/>.
/// </summary>
public class DenFightRuleTests
{
    private const int Beside = 1;
    private static readonly Point3D InTheDen = PkRules.BucsDenHaven;
    private static readonly Point3D OutOfTheDen = new(1000, 1000, 0);
    private static uint _nextSerial = 0xA601;

    static DenFightRuleTests() => Timer.Init(0);

    public DenFightRuleTests() => TestMap.EnsureInternal();

    [Fact]
    public void ABlueInTheDen_DrawsOnNoRedThatHurtsNoFriend()
    {
        var blue = Person(InTheDen);
        var red = Red(Person(Near(InTheDen)));
        red.Combatant = Person(Near(InTheDen));

        Assert.False(WorldPlay.OutlawOnFriend(blue, red));
        Assert.False(WorldPlay.MayStartFightInDen(blue, red));
        Assert.False(FollowSkill.TakesLeadersFoe(blue, red));
    }

    [Fact]
    public void ABlueInTheDen_AnswersARedOnItself()
    {
        var blue = Person(InTheDen);
        var red = Red(Person(Near(InTheDen)));
        red.Combatant = blue;

        Assert.True(WorldPlay.OutlawOnFriend(blue, red));
        Assert.True(WorldPlay.MayStartFightInDen(blue, red));
    }

    [Fact]
    public void ABlueInTheDen_AnswersAGrayOnItself_NotABlueFoe()
    {
        var blue = Person(InTheDen);
        var gray = Person(Near(InTheDen));
        gray.Criminal = true;
        gray.Combatant = blue;
        var blueFoe = Person(Near(InTheDen));
        blueFoe.Combatant = blue;

        Assert.True(WorldPlay.MayStartFightInDen(blue, gray));
        Assert.False(WorldPlay.MayStartFightInDen(blue, blueFoe));
    }

    [Fact]
    public void ABlueDrawsOnNobodyInTheDenFromOutside()
    {
        var blue = Person(OutOfTheDen);
        var red = Red(Person(InTheDen));

        Assert.False(WorldPlay.MayStartFightInDen(blue, red));
    }

    [Fact]
    public void OutOfTheDen_TheRuleHoldsNobodyBack()
    {
        var blue = Person(OutOfTheDen);
        var red = Red(Person(Near(OutOfTheDen)));

        Assert.True(WorldPlay.MayStartFightInDen(blue, red));
        Assert.True(FollowSkill.TakesLeadersFoe(blue, red));
    }

    [Fact]
    public void ARedIsNotHeldToTheBlueRule()
    {
        var red = Red(Person(InTheDen));
        var blue = Person(Near(InTheDen));

        Assert.True(WorldPlay.MayStartFightInDen(red, blue));
    }

    [Fact]
    public void ARedInTheDen_DrawsOnNoOtherRed()
    {
        var red = Red(Person(InTheDen));
        var otherRed = Red(Person(Near(InTheDen)));

        Assert.False(WorldPlay.MayStartFightInDen(red, otherRed));
    }

    [Fact]
    public void OutOfTheDen_ARedMayDrawOnARed()
    {
        var red = Red(Person(OutOfTheDen));
        var otherRed = Red(Person(Near(OutOfTheDen)));

        Assert.True(WorldPlay.MayStartFightInDen(red, otherRed));
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void RedMayDrawInDen_OnlyNotOnAnotherRedAtHome(bool inDen, bool foeIsRed, bool mayDraw) =>
        Assert.Equal(mayDraw, LawfulRules.RedMayDrawInDen(inDen, foeIsRed));

    private static Point3D Near(Point3D spot) => new(spot.X + Beside, spot.Y, spot.Z);

    private static SosariaCharacter Red(SosariaCharacter character)
    {
        character.Kills = PkRules.MurdersToRed;
        return character;
    }

    private static SosariaCharacter Person(Point3D at)
    {
        var person = new SosariaCharacter((Serial)_nextSerial++);
        person.DefaultMobileInit();
        person.Location = at;
        return person;
    }
}
