using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Deliberation;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A hidden mobile is not there for a character that could not see it under the engine's own
/// rule: a hidden GM is not heard, named, answered, traded with or counted as a body near.
/// A visible GM stays visible, as a visible person does.
/// </summary>
public class PerceptionTests : IDisposable
{
    private const int SpotX = 100;
    private const int SpotY = 100;
    private const int NextTile = 1;
    private const string OperatorName = "Operator";
    private static readonly TimeSpan WantOpen = TimeSpan.FromMinutes(1);

    private readonly List<Mobile> _placed = [];

    static PerceptionTests() => Timer.Init(0);

    public PerceptionTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
    }

    public void Dispose() => TestMap.Remove(_placed);

    [Fact]
    public void Perceives_HiddenGameMaster_IsNotSeenByACharacter()
    {
        var character = Character((Serial)0x5E01);
        var operatorGm = Staff((Serial)0x5E02, hidden: true);

        Assert.False(People.Perceives(character, operatorGm));
    }

    [Fact]
    public void Perceives_VisibleGameMaster_IsSeen()
    {
        var character = Character((Serial)0x5E03);
        var operatorGm = Staff((Serial)0x5E04, hidden: false);

        Assert.True(People.Perceives(character, operatorGm));
    }

    [Fact]
    public void Perceives_VisiblePerson_IsSeen()
    {
        var character = Character((Serial)0x5E05);
        var person = Person((Serial)0x5E06);

        Assert.True(People.Perceives(character, person));
    }

    [Fact]
    public void Perceives_HiddenPerson_IsNotSeenByACharacter_ButIsByStaff()
    {
        var character = Character((Serial)0x5E07);
        var watcher = Staff((Serial)0x5E08, hidden: false);
        var person = Person((Serial)0x5E09);
        person.Hidden = true;

        Assert.False(People.Perceives(character, person));
        Assert.True(People.Perceives(watcher, person));
    }

    [Fact]
    public void Perceives_SelfAndNull()
    {
        var character = Character((Serial)0x5E0A);
        character.Hidden = true;

        Assert.True(People.Perceives(character, character));
        Assert.False(People.Perceives(character, null));
        Assert.False(People.Perceives(null, character));
    }

    [Fact]
    public void Hears_HiddenGameMaster_IsNotHeard_VisibleIs()
    {
        var character = Character((Serial)0x5E0B);
        var operatorGm = Staff((Serial)0x5E0C, hidden: true);

        Assert.False(SpeechResponder.Hears(character, operatorGm));

        operatorGm.Hidden = false;

        Assert.True(SpeechResponder.Hears(character, operatorGm));
    }

    [Fact]
    public void NearbyName_NamesOnlyAVisibleGameMaster()
    {
        var character = Character((Serial)0x5E0D);
        var operatorGm = Staff((Serial)0x5E0E, hidden: true);
        operatorGm.Name = OperatorName;

        Assert.Null(Brain.NearbyName(character));

        operatorGm.Hidden = false;

        Assert.Equal(OperatorName, Brain.NearbyName(character));
    }

    [Fact]
    public void HasOccupant_AHiddenGameMasterOnTheVein_DoesNotHoldIt()
    {
        var miner = Character((Serial)0x5E0F);
        var operatorGm = Staff((Serial)0x5E10, hidden: true);

        Assert.False(HarvestOccupancy.HasOccupant(miner, operatorGm.Location));

        operatorGm.Hidden = false;

        Assert.True(HarvestOccupancy.HasOccupant(miner, operatorGm.Location));
    }

    [Fact]
    public void WantAnswered_AHiddenGameMaster_GetsNoBuyer()
    {
        var buyer = Character((Serial)0x5E11);
        var operatorGm = Staff((Serial)0x5E12, hidden: true);
        var claim = new GoodsClaim(Appraisal.RowByKey("polearm"), 1, true, Appraisal.NoMagic);
        TradeMarket.PostWant(buyer, claim, Core.Now + WantOpen);

        try
        {
            Assert.Null(TradeMarket.WantAnswered(operatorGm, null));

            operatorGm.Hidden = false;

            Assert.Equal(buyer, TradeMarket.WantAnswered(operatorGm, null)?.Buyer);
        }
        finally
        {
            TradeMarket.DropWant(buyer);
        }
    }

    private SosariaCharacter Character(Serial serial)
    {
        var character = new SosariaCharacter(serial);
        Place(character, SpotX);
        return character;
    }

    private PlayerMobile Person(Serial serial)
    {
        var person = new PlayerMobile(serial);
        Place(person, SpotX + NextTile);
        return person;
    }

    private PlayerMobile Staff(Serial serial, bool hidden)
    {
        var staff = Person(serial);
        staff.AccessLevel = AccessLevel.GameMaster;
        staff.Hidden = hidden;
        return staff;
    }

    private void Place(Mobile mobile, int x)
    {
        mobile.DefaultMobileInit();
        mobile.MoveToWorld(new Point3D(x, SpotY, 0), TestMap.EnsureLand());
        _placed.Add(mobile);
    }
}
