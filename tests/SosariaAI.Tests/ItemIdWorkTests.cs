using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Misc;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// Item Identification on real pieces: a person with the skill names its own unidentified
/// piece, and a merchant names a customer's for the fee, paid only for a named piece. At
/// grandmaster the engine's check always passes, and at no skill it never does. The engine's
/// own target check (SkillCheck) is put in place for these tests: the collection runs alone,
/// and the handler found before is put back.
/// </summary>
[Collection(EngineGuildsCollection.Name)]
public class ItemIdWorkTests : IDisposable
{
    private const double Grandmaster = 100;
    private const double NoSkill = 0;
    private const int Purse = 100;
    private const int Next = 1;
    private static readonly Point3D Counter = new(36, 116, 0);
    private static uint _nextSerial = 0x7601;

    private readonly List<IEntity> _placed = [];
    private readonly SkillCheckTargetHandler _handlerBefore = Mobile.SkillCheckTargetHandler;

    static ItemIdWorkTests() => Timer.Init(0);

    public ItemIdWorkTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
        TestSkills.EnsureTable();
        Mobile.SkillCheckTargetHandler = SkillCheck.Mobile_SkillCheckTarget;
    }

    public void Dispose()
    {
        Mobile.SkillCheckTargetHandler = _handlerBefore;
        TestMap.Remove(_placed);
    }

    [Fact]
    public void ShowsUnidentified_OnlyMagicNobodyNamed()
    {
        var vanq = new Katana { DamageLevel = WeaponDamageLevel.Vanq };
        var plain = new Katana();
        var guarding = new PlateChest { ProtectionLevel = ArmorProtectionLevel.Guarding };

        try
        {
            Assert.True(ItemIdWork.ShowsUnidentified(vanq));
            Assert.True(ItemIdWork.ShowsUnidentified(guarding));
            Assert.False(ItemIdWork.ShowsUnidentified(plain));

            vanq.Identified = true;

            Assert.False(ItemIdWork.ShowsUnidentified(vanq));
        }
        finally
        {
            vanq.Delete();
            plain.Delete();
            guarding.Delete();
        }
    }

    [Fact]
    public void Rep_APersonWithTheSkillNamesItsOwnPiece()
    {
        var person = Person(Counter, Grandmaster);
        var piece = Unnamed(person);

        Assert.True(ItemIdWork.Rep(person));
        Assert.True(piece.Identified);
        Assert.Null(ItemIdWork.FirstUnidentified(person));
        Assert.False(ItemIdWork.Rep(person));
    }

    [Fact]
    public void Rep_AMerchantNamesACustomersPieceForTheFee()
    {
        var merchant = Person(Counter, Grandmaster);
        var customer = Person(new Point3D(Counter.X + Next, Counter.Y, Counter.Z), NoSkill);
        var piece = Unnamed(customer);
        customer.Backpack.DropItem(new Gold(Purse));

        Assert.True(ItemIdWork.Rep(merchant));
        Assert.True(piece.Identified);
        Assert.Equal(Purse - ItemIdRules.IdFee, customer.Backpack.GetAmount(typeof(Gold)));
        Assert.Equal(ItemIdRules.IdFee, merchant.Backpack.GetAmount(typeof(Gold)));
    }

    [Fact]
    public void Rep_NoFeeForAPieceTheMerchantCouldNotName()
    {
        var merchant = Person(Counter, ItemIdRules.ServiceMinSkill);
        merchant.Skills.ItemID.Base = NoSkill;
        var customer = Person(new Point3D(Counter.X + Next, Counter.Y, Counter.Z), NoSkill);
        Unnamed(customer);
        customer.Backpack.DropItem(new Gold(Purse));

        Assert.False(ItemIdWork.Rep(merchant));
        Assert.Equal(Purse, customer.Backpack.GetAmount(typeof(Gold)));
    }

    private static Katana Unnamed(SosariaCharacter owner)
    {
        var piece = new Katana { DamageLevel = WeaponDamageLevel.Vanq };
        owner.Backpack.DropItem(piece);
        return piece;
    }

    private SosariaCharacter Person(Point3D at, double itemId)
    {
        var character = new SosariaCharacter((Serial)_nextSerial++);
        character.DefaultMobileInit();
        character.AddItem(new Backpack());
        character.Skills.ItemID.Base = itemId;
        character.MoveToWorld(at, TestMap.EnsureLand());
        _placed.Add(character);
        return character;
    }
}
