using Server;
using SosariaAI.Combat;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A tank mage swings and casts: its kit holds its weapon, the spellbook and every reagent.</summary>
public class ClassKitsTests
{
    private const string Halberd = "Halberd";

    private static ClassBuildTemplate TankMage() =>
        new()
        {
            Primary = SkillName.Magery,
            Secondary = [SkillName.Swords, SkillName.Tactics, SkillName.MagicResist],
            Style = CombatStyle.Melee,
            Weapon = Halberd,
            TankMage = true
        };

    [Fact]
    public void CastsInMelee_OnlyTheTankMage()
    {
        Assert.True(ClassKits.CastsInMelee(TankMage()));
        Assert.False(ClassKits.CastsInMelee(ClassBuilds.TemplateFor(PersonClass.Warrior, CharacterRole.Fighter, "Felucca:k#1")));
        Assert.False(ClassKits.CastsInMelee(ClassBuilds.TemplateFor(PersonClass.Mage, CharacterRole.Fighter, "Felucca:k#1")));
    }

    [Fact]
    public void TankMage_CarriesTheSpellbookAndEveryReagent()
    {
        var kit = ClassKits.For(TankMage(), SkillTier.Master, female: false, "Felucca:k#1");

        Assert.Contains(ClassKits.Spellbook, kit);

        foreach (var reagent in ClassKits.AllReagents)
        {
            Assert.Contains(reagent, kit);
        }
    }
}
