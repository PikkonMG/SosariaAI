using Server;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A better weapon or chest piece makes a character stronger. A fighter who bought plate
/// over chain used to read exactly as strong as before.
/// </summary>
public class GearScoreTests
{
    static GearScoreTests() => Timer.Init(0);

    public GearScoreTests() => TestMap.EnsureInternal();

    [Fact]
    public void Of_PlateOverChain_ScoresHigher()
    {
        var chain = Character((Serial)0x7A01);
        Wear(chain, new ChainChest((Serial)0x7A11) { Layer = Layer.InnerTorso });
        var plate = Character((Serial)0x7A02);
        Wear(plate, new PlateChest((Serial)0x7A12) { Layer = Layer.InnerTorso });

        Assert.Equal(GearLadder.PlateScore - GearLadder.ChainmailScore, GearScore.Of(plate) - GearScore.Of(chain));
    }

    [Fact]
    public void Of_BroadswordOverKatana_ScoresHigher()
    {
        var katana = Character((Serial)0x7A03);
        Wear(katana, new Katana((Serial)0x7A13) { Layer = Layer.OneHanded });
        var broadsword = Character((Serial)0x7A04);
        Wear(broadsword, new Broadsword((Serial)0x7A14) { Layer = Layer.OneHanded });

        Assert.Equal(GearPlan.KatanaGearScore, GearScore.Of(katana));
        Assert.Equal(GearPlan.BroadswordGearScore, GearScore.Of(broadsword));
    }

    [Fact]
    public void Of_Spellbook_CountsAsAPlainWeapon()
    {
        var mage = Character((Serial)0x7A05);
        Wear(mage, new Spellbook((Serial)0x7A15) { Layer = Layer.OneHanded });

        Assert.Equal(GearScore.WeaponScore, GearScore.Of(mage));
    }

    [Fact]
    public void RankOf_ExceptionalWork_OutranksTheSamePlainPiece()
    {
        var plain = new Katana((Serial)0x7A20);
        var exceptional = new Katana((Serial)0x7A21) { Quality = WeaponQuality.Exceptional };

        Assert.Equal(GearScore.RankOf(plain) + GearScore.ExceptionalBonus, GearScore.RankOf(exceptional));
    }

    [Fact]
    public void RankOf_MagicKatana_OutranksAPlainBroadsword()
    {
        var vanquishing = new Katana((Serial)0x7A22) { DamageLevel = WeaponDamageLevel.Vanq };

        Assert.True(GearScore.RankOf(vanquishing) > GearPlan.BroadswordGearScore);
        Assert.Equal((int)WeaponDamageLevel.Vanq, GearScore.MagicLevel(vanquishing));
    }

    [Fact]
    public void RankOf_MagicArmor_CountsItsProtection()
    {
        var guarding = new RingmailChest((Serial)0x7A23) { ProtectionLevel = ArmorProtectionLevel.Guarding };

        Assert.Equal(
            GearLadder.RingmailScore + (int)ArmorProtectionLevel.Guarding * GearScore.MagicLevelBonus,
            GearScore.RankOf(guarding)
        );
    }

    [Fact]
    public void TorsoRank_ExceptionalStudded_MatchesPlainChain()
    {
        var character = Character((Serial)0x7A06);
        Wear(character, new StuddedChest((Serial)0x7A16) { Layer = Layer.InnerTorso, Quality = ArmorQuality.Exceptional });

        Assert.Equal(GearLadder.ChainmailScore, GearScore.TorsoRank(character));
    }

    [Fact]
    public void TorsoRank_DeathRobe_IsNoArmor()
    {
        var character = Character((Serial)0x7A09);
        Wear(character, new DeathRobe((Serial)0x7A19) { Layer = Layer.OuterTorso });

        Assert.Equal(GearScore.UnarmedScore, GearScore.TorsoRank(character));
    }

    [Fact]
    public void SlotRank_ReadsArmorOnly()
    {
        var character = Character((Serial)0x7A0A);
        Wear(character, new PlateLegs((Serial)0x7A1A) { Layer = Layer.Pants });
        Wear(character, new FeatheredHat((Serial)0x7A1B) { Layer = Layer.Helm });

        Assert.Equal(GearLadder.PlateScore, GearScore.SlotRank(character, GearSlot.Legs));
        // A hat is cloth: the helm slot still reads empty, so a helm is worth buying.
        Assert.Equal(GearScore.UnarmedScore, GearScore.SlotRank(character, GearSlot.Helm));
        Assert.Equal(GearScore.UnarmedScore, GearScore.SlotRank(character, GearSlot.Chest));
    }

    private static SosariaCharacter Character(Serial serial)
    {
        var character = new SosariaCharacter(serial);
        character.DefaultMobileInit();
        return character;
    }

    private static void Wear(SosariaCharacter character, Item item)
    {
        item.Parent = character;
        character.Items.Add(item);
    }
}
