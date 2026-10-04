using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SosariaAI.Behaviour;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class TalkLibraryTests
{
    private const int MinCategories = 90;
    private const int MaxCategories = 140;
    private const int MinLinesPerCategory = 10;
    private const string Category = "test_talk";

    private static IReadOnlyList<string> CategoryConstants()
    {
        var names = new List<string>();

        foreach (var field in typeof(TalkCategory).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral && field.GetRawConstantValue() is string name)
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static TalkLibrary LibraryOf(params string[] rows) =>
        new(new Dictionary<string, List<TalkLine>> { [Category] = TalkFile.Parse(rows).Lines });

    [Fact]
    public void Defaults_CoverEveryCategoryOnce()
    {
        var topics = new HashSet<string>(StringComparer.Ordinal);

        foreach (var topic in TalkDefaults.All)
        {
            Assert.True(topics.Add(topic.Name), topic.Name);
        }

        var constants = CategoryConstants();
        Assert.Equal(constants.Count, topics.Count);

        foreach (var name in constants)
        {
            Assert.Contains(name, topics);
        }

        Assert.InRange(topics.Count, MinCategories, MaxCategories);
    }

    [Fact]
    public void Defaults_HaveTenLinesOrMore_NoProblems_NoEmotes()
    {
        foreach (var topic in TalkDefaults.All)
        {
            Assert.True(topic.Lines.Length >= MinLinesPerCategory, topic.Name);
            Assert.False(string.IsNullOrWhiteSpace(topic.About), topic.Name);

            var parsed = TalkFile.Parse(topic.Lines);
            Assert.Empty(parsed.Problems);
            Assert.Equal(topic.Lines.Length, parsed.Lines.Count);

            foreach (var line in parsed.Lines)
            {
                // Only the emotes category wraps its lines in *stars*; all the rest are spoken.
                Assert.Equal(topic.Name == TalkCategory.Emotes, line.Text.StartsWith('*'));
            }
        }
    }

    [Fact]
    public void Pick_SkipsLinesThatNameAnEmptySlot()
    {
        var library = LibraryOf("come here {foe}", "attack!");

        Assert.Equal("attack!", library.Pick(Category, 0, default, EraBand.T2A));
        Assert.Equal("come here lich", library.Pick(Category, 0, new TalkSlots { Foe = "lich" }, EraBand.T2A));
    }

    [Fact]
    public void Pick_NothingFits_IsNull()
    {
        var library = LibraryOf("come here {foe}");

        Assert.Null(library.Pick(Category, 0, default, EraBand.T2A));
        Assert.Null(library.Pick("no_such_category", 0, default, EraBand.T2A));
        Assert.Null(library.Pick(null, 0, default, EraBand.T2A));
    }

    [Fact]
    public void Pick_SameRollSameLine_NegativeRollStaysInside()
    {
        var library = LibraryOf("a", "b", "c");

        Assert.Equal(library.Pick(Category, 4, default, EraBand.ML), library.Pick(Category, 4, default, EraBand.ML));
        Assert.Contains(library.Pick(Category, int.MinValue, default, EraBand.ML), new[] { "a", "b", "c" });
    }

    [Fact]
    public void Pick_KeepsEraLinesToTheirEra()
    {
        var library = LibraryOf("[eras: t2a] pls be vanq", "gold please gold");

        Assert.Equal("pls be vanq", library.Pick(Category, 0, default, EraBand.T2A));
        Assert.Equal("gold please gold", library.Pick(Category, 0, default, EraBand.ML));
        Assert.Equal("gold please gold", library.Pick(Category, 1, default, EraBand.Modern));
    }

    [Fact]
    public void Parse_SectionEras_AllEndsThem()
    {
        var parsed = TalkFile.Parse(
        [
            "# a note",
            string.Empty,
            "[eras: ml,modern]",
            "arties",
            "[eras: all]",
            "anytime"
        ]);

        Assert.Equal(2, parsed.Lines.Count);
        Assert.Equal(["ml", "modern"], parsed.Lines[0].Eras);
        Assert.Empty(parsed.Lines[1].Eras);
        Assert.Empty(parsed.Problems);
    }

    [Fact]
    public void Parse_ReportsUnknownEraSlotAndOpenTag()
    {
        var parsed = TalkFile.Parse(["[eras: uor] old times", "hi {nmae}", "[eras: ml broken"]);

        Assert.Equal(3, parsed.Problems.Count);
        Assert.Equal(2, parsed.Lines.Count);
        Assert.Equal(TalkSlot.Unknown, parsed.Lines[1].Needs & TalkSlot.Unknown);
    }

    [Fact]
    public void Render_ParsesBackToTheSameLines()
    {
        foreach (var topic in TalkDefaults.All)
        {
            var rendered = TalkFile.Parse(TalkFile.Render(topic).Split('\n')).Lines;
            var direct = TalkFile.Parse(topic.Lines).Lines;

            Assert.Equal(direct.Count, rendered.Count);

            for (var i = 0; i < direct.Count; i++)
            {
                Assert.Equal(direct[i].Text, rendered[i].Text);
                Assert.Equal(direct[i].Eras, rendered[i].Eras);
            }
        }
    }

    [Fact]
    public void LoadOrCreate_WritesMissingFiles_AndKeepsTheOperatorsEdits()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sosaria-talk-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(dir);
            var edited = Path.Combine(dir, TalkCategory.Wts + TalkFile.Extension);
            File.WriteAllLines(edited, ["# mine", "only mine {item} {price}"]);

            var library = TalkLibrary.LoadOrCreate(dir);

            Assert.Equal(
                "only mine katana 5k",
                library.Pick(TalkCategory.Wts, 0, new TalkSlots { Item = "katana", Price = "5k" }, EraBand.T2A)
            );
            Assert.Equal(["# mine", "only mine {item} {price}"], File.ReadAllLines(edited));
            Assert.Equal(TalkDefaults.All.Count, Directory.GetFiles(dir, "*" + TalkFile.Extension).Length);
            Assert.Equal("gl", library.Pick(TalkCategory.DuelAccept, 0, default, EraBand.T2A));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Slots_FillAndReportWhatTheyHold()
    {
        var slots = new TalkSlots { Name = "Mira", Count = 3 };

        Assert.Equal(TalkSlot.Name | TalkSlot.Count, slots.Filled);
        Assert.Equal("Mira has 3", slots.Fill("{name} has {count}"));
        Assert.Equal(TalkSlot.Foe | TalkSlot.Place, TalkSlots.Needs("{foe} at {place}"));
        Assert.Equal("{dungeon}", TalkSlots.TokenOf(TalkSlot.Dungeon));
    }

    [Fact]
    public void Odds_UnheardSpeaksFarLess()
    {
        Assert.Equal(TalkOdds.EngagePercent, TalkOdds.Chance(TalkOdds.EngagePercent, heard: true));
        Assert.Equal(TalkOdds.EngagePercent / TalkOdds.UnheardDivisor, TalkOdds.Chance(TalkOdds.EngagePercent, heard: false));
    }

    [Fact]
    public void Words_DropTheArticle()
    {
        Assert.Equal("lich", TalkWords.WithoutArticle("a lich"));
        Assert.Equal("orc captain", TalkWords.WithoutArticle("An Orc Captain"));
        Assert.Equal("dread spider", TalkWords.WithoutArticle("the dread spider"));
        Assert.Equal("mongbat", TalkWords.WithoutArticle("mongbat"));
        Assert.Null(TalkWords.WithoutArticle(" "));
    }

    [Fact]
    public void WorkCategory_FollowsTheSkill()
    {
        Assert.Equal(TalkCategory.MiningTalk, AmbientTalk.WorkCategory(Configuration.SkillKinds.Mine));
        Assert.Equal(TalkCategory.CraftTalk, AmbientTalk.WorkCategory(Configuration.SkillKinds.Cook));
        Assert.Equal(TalkCategory.Traveling, AmbientTalk.WorkCategory(Configuration.SkillKinds.Travel));
        Assert.Null(AmbientTalk.WorkCategory(Configuration.SkillKinds.Steal));
        Assert.Null(AmbientTalk.WorkCategory(null));
    }

    [Fact]
    public void AlignmentFight_PicksTheSidesLines()
    {
        Assert.Equal(TalkCategory.ChaosBattle, WorldPlay.AlignmentFightCategory(Server.Guilds.GuildType.Chaos));
        Assert.Equal(TalkCategory.OrderBattle, WorldPlay.AlignmentFightCategory(Server.Guilds.GuildType.Order));
    }
}
