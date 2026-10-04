using System.Linq;
using Server;
using Server.Items;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using SosariaAI.Tests.RealMap;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A character already in the save gets the rune kit once on its next bind: the real pack,
/// the real runebook, the saved flag that keeps it to once. A novice keeps walking. Loose
/// marked runes and a scroll stack from an older save go into a runebook.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RuneKitTopUpTests
{
    private static readonly Point3D BritainFields = new(1350, 1800, 0);
    private static readonly Point3D[] OldMarks = [new(1434, 1699, 0), new(1176, 2635, 0), new(2499, 919, 0)];
    private const double MageryToTravel = 60;
    private const int OldScrollStack = 12;

    public RuneKitTopUpTests()
    {
        if (RealMapWorld.Available)
        {
            KitWorld.Ensure();
        }
    }

    [RealMapFact]
    public void AnEstablishedTraveler_GetsItsMarkedRunesOnce()
    {
        var character = Returning(SkillTier.Journeyman, "Felucca:RuneTopUpJourneyman#1");

        RuneKit.Pack(character, firstDay: false);
        var book = RuneShelf.Book(character);

        Assert.True(character.RuneKitPacked);
        Assert.NotNull(book);
        Assert.Contains(book.Entries, entry => entry.Map == Map.Felucca);
        Assert.DoesNotContain(Runes(character), rune => rune.Marked);

        var entries = book.Entries.Count;
        RuneKit.Pack(character, firstDay: false);

        Assert.Same(book, RuneShelf.Book(character));
        Assert.Equal(entries, book.Entries.Count);
    }

    [RealMapFact]
    public void LooseRunesAndAScrollStack_GoIntoARunebookOnce()
    {
        var character = Returning(SkillTier.Journeyman, "Felucca:RuneTopUpShelve#1");
        character.Skills.Magery.Base = MageryToTravel;
        character.RuneKitPacked = true;

        for (var i = 0; i < OldMarks.Length; i++)
        {
            character.AddToBackpack(new RecallRune { Target = OldMarks[i], TargetMap = Map.Felucca, Marked = true });
        }

        character.AddToBackpack(new RecallScroll(OldScrollStack));

        RuneKit.Pack(character, firstDay: false);
        var book = RuneShelf.Book(character);

        Assert.NotNull(book);
        Assert.Equal(OldMarks.Length, book.Entries.Count);
        Assert.DoesNotContain(Runes(character), rune => rune.Marked);
        Assert.Equal(book.MaxCharges, book.CurCharges);
        Assert.True(character.Backpack.GetAmount(typeof(RecallScroll)) < OldScrollStack - book.MaxCharges);

        RuneKit.Pack(character, firstDay: false);

        Assert.Single(character.Backpack.Items.OfType<Runebook>());
        Assert.Equal(OldMarks.Length, book.Entries.Count);
    }

    [RealMapFact]
    public void ANovice_KeepsWalking_AndIsJudgedOnce()
    {
        var character = Returning(SkillTier.Novice, "Felucca:RuneTopUpNovice#1");

        RuneKit.Pack(character, firstDay: false);

        Assert.True(character.RuneKitPacked);
        Assert.DoesNotContain(Runes(character), rune => rune.Marked);
    }

    /// <summary>A warrior bound as one already in the world: build and kit on, no first-day finish.</summary>
    private static SosariaCharacter Returning(SkillTier tier, string id)
    {
        var profile = KitWorld.Profile(PersonClass.Warrior, tier, PersonWealth.Modest, female: false);
        var character = KitWorld.Blank(id, profile.Female);
        character.BindProfile(profile);
        character.ApplyFreshBuild();
        character.HomeFacet = FacetNames.Felucca;
        character.HomeSpawn = BritainFields;
        return character;
    }

    private static RecallRune[] Runes(SosariaCharacter character) =>
        character.Backpack.Items.OfType<RecallRune>().ToArray();
}
