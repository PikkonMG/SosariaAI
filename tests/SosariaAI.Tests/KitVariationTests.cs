using System.Collections.Generic;
using Server.Items;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class KitVariationTests
{
    private static readonly List<string> SwordsmanKit =
        ["Katana", "MetalShield", "ChainChest", "ChainLegs", "ChainCoif", "LeatherGloves", "Boots"];

    private static readonly List<string> ArcherKit =
        ["Bow", "Arrow", "LeatherChest", "LeatherLegs", "LeatherGorget", "LeatherGloves", "LeatherCap", "Boots"];

    [Fact]
    public void Vary_SamePersonSameKit()
    {
        var first = KitVariation.Vary(SwordsmanKit, "Felucca:bran#4");
        var again = KitVariation.Vary(SwordsmanKit, "Felucca:bran#4");

        Assert.Equal(first, again);
    }

    [Fact]
    public void Vary_ManyPeopleSpreadOverTheWeaponTable()
    {
        var weapons = new HashSet<string>();

        for (var i = 1; i <= 60; i++)
        {
            weapons.Add(KitVariation.Vary(SwordsmanKit, $"Felucca:dunn#{i}")[0]);
        }

        Assert.True(weapons.Count >= 4, $"expected spread, got {string.Join(",", weapons)}");
        Assert.DoesNotContain("Bow", weapons);
        Assert.DoesNotContain("Club", weapons);
    }

    [Fact]
    public void Vary_CrossbowPicksCarryBoltsNotArrows()
    {
        var sawCrossbow = false;

        for (var i = 1; i <= 120 && !sawCrossbow; i++)
        {
            var kit = KitVariation.Vary(ArcherKit, $"Felucca:tam#{i}");

            if (kit[0] is "Crossbow" or "HeavyCrossbow")
            {
                sawCrossbow = true;
                Assert.Contains("Bolt", kit);
                Assert.DoesNotContain("Arrow", kit);
            }
        }

        Assert.True(sawCrossbow, "no copy rolled a crossbow in 120 picks");
    }

    [Fact]
    public void Vary_BowPicksKeepArrows()
    {
        for (var i = 1; i <= 60; i++)
        {
            var kit = KitVariation.Vary(ArcherKit, $"Felucca:orla#{i}");

            if (kit[0] == "Bow")
            {
                Assert.Contains("Arrow", kit);
                Assert.DoesNotContain("Bolt", kit);
            }
        }
    }

    [Fact]
    public void Vary_KitWithoutAKnownWeaponIsUntouched()
    {
        var kit = new List<string> { "Spellbook", "Robe", "Boots" };
        Assert.Equal(kit, KitVariation.Vary(kit, "Felucca:sela#2"));
    }

    [Fact]
    public void Vary_NeverMutatesThePresetList()
    {
        var original = new List<string>(SwordsmanKit);
        KitVariation.Vary(SwordsmanKit, "Felucca:kerr#9");
        Assert.Equal(original, SwordsmanKit);
    }

    [Fact]
    public void Vary_EmptyKitAndBlankIdAreSafe()
    {
        Assert.Empty(KitVariation.Vary(null, "Felucca:x#1"));
        Assert.Empty(KitVariation.Vary([], "Felucca:x#1"));
        Assert.Equal(SwordsmanKit, KitVariation.Vary(SwordsmanKit, ""));
    }

    [Fact]
    public void InRow_KnowsEveryPieceTheRowRolls()
    {
        Assert.True(KitVariation.InRow(KitVariation.OneHandedSword, "Scimitar"));
        Assert.True(KitVariation.InRow(KitVariation.OneHandedSword, KitVariation.OneHandedSword));
        Assert.True(KitVariation.InRow(KitVariation.HerdingStaff, "ShepherdsCrook"));
        Assert.False(KitVariation.InRow(KitVariation.OneHandedSword, "Kryss"));
        Assert.False(KitVariation.InRow(null, "Kryss"));
        Assert.False(KitVariation.InRow(KitVariation.OneHandedSword, null));
    }

    [Fact]
    public void SameAmmunition_ABowAndACrossbowDoNotShareAQuiver()
    {
        Assert.True(KitVariation.SameAmmunition("Crossbow", "HeavyCrossbow"));
        Assert.True(KitVariation.SameAmmunition("Katana", "Broadsword"));
        Assert.False(KitVariation.SameAmmunition("Bow", "Crossbow"));
        Assert.False(KitVariation.SameAmmunition("Bow", null));
    }
}
