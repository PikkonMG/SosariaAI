using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class CharacterCommandsTests
{
    private const string ConnorName = "Connor";
    private const string BoatTarget = "a small boat";
    private const string HuntActivity = "Hunt";
    private const string BritainLocation = "(1420, 1695, 20)";
    private const string FeluccaMap = "Felucca";
    private const int VeteranPower = 117;
    private const int FortyTiles = 40;
    private const int AfternoonHour = 14;
    private const int ActiveStart = 6;
    private const int ActiveEnd = 20;
    private const int GoldProgress = 400;
    private const string ConnorId = "bot:Felucca:connor";
    private const string StaffId = "player:0x1";
    private const int NothingShared = 0;
    private const int HealedScore = 25;
    private const string HealedReason = "healed me";

    [Fact]
    public void CommandNames_AreStableSoTheOperatorCanLearnThem()
    {
        Assert.Equal("Sosaria", CharacterCommands.ListCommand);
        Assert.Equal("SosariaGo", CharacterCommands.GoToCommand);
        Assert.Equal("SosariaBring", CharacterCommands.BringCommand);
        Assert.Equal("SosariaWatch", CharacterCommands.WatchCommand);
        Assert.Equal("SosariaSay", CharacterCommands.SayCommand);
        Assert.Equal("SosariaMemory", CharacterCommands.MemoryCommand);
    }

    [Fact]
    public void GoAndBring_AreDistinctFromTheListCommand()
    {
        Assert.NotEqual(CharacterCommands.ListCommand, CharacterCommands.GoToCommand);
        Assert.NotEqual(CharacterCommands.ListCommand, CharacterCommands.BringCommand);
        Assert.NotEqual(CharacterCommands.GoToCommand, CharacterCommands.BringCommand);
        Assert.NotEqual(CharacterCommands.WatchCommand, CharacterCommands.SayCommand);
        Assert.NotEqual(CharacterCommands.WatchCommand, CharacterCommands.ListCommand);
    }

    [Fact]
    public void GoAndBring_StartWithTheListCommandSoTheyGroupTogether()
    {
        Assert.StartsWith(CharacterCommands.ListCommand, CharacterCommands.GoToCommand);
        Assert.StartsWith(CharacterCommands.ListCommand, CharacterCommands.BringCommand);
        Assert.StartsWith(CharacterCommands.ListCommand, CharacterCommands.WatchCommand);
        Assert.StartsWith(CharacterCommands.ListCommand, CharacterCommands.SayCommand);
    }

    [Fact]
    public void AmbitionBrief_IncludesKindTargetAndProgress()
    {
        var ambition = new Ambition(
            AmbitionKind.Gold,
            BoatTarget,
            AmbitionRules.DefaultBoatGold,
            GoldProgress
        );

        var text = CharacterCommandText.AmbitionBrief(ambition);

        Assert.Contains(nameof(AmbitionKind.Gold), text);
        Assert.Contains(BoatTarget, text);
        Assert.Contains($"{GoldProgress}/{AmbitionRules.DefaultBoatGold}", text);
        Assert.Equal(AmbitionRules.DescribeNone, CharacterCommandText.AmbitionBrief(new Ambition(AmbitionKind.None, "", 0, 0)));
    }

    [Fact]
    public void ListLine_IncludesAmbitionOnTheSameLine()
    {
        var ambition = new Ambition(
            AmbitionKind.Gold,
            BoatTarget,
            AmbitionRules.DefaultBoatGold,
            GoldProgress
        );
        var line = CharacterCommandText.ListLine(
            ConnorName,
            HuntActivity,
            BritainLocation,
            FeluccaMap,
            VeteranPower,
            CharacterCommandText.DistanceLabel(true, FortyTiles),
            ghost: false,
            ambition
        );

        Assert.Contains(ConnorName, line);
        Assert.Contains("(connor)", CharacterCommandText.ListLine(
            ConnorName,
            HuntActivity,
            BritainLocation,
            FeluccaMap,
            VeteranPower,
            CharacterCommandText.DistanceLabel(true, FortyTiles),
            ghost: false,
            ambition,
            "connor"
        ));
        Assert.Contains(HuntActivity, line);
        Assert.Contains(BritainLocation, line);
        Assert.Contains(FeluccaMap, line);
        Assert.Contains(VeteranPower.ToString(), line);
        Assert.Contains($"{FortyTiles}{CharacterCommandText.TilesSuffix}", line);
        Assert.Contains(BoatTarget, line);
        Assert.Contains($"{GoldProgress}/{AmbitionRules.DefaultBoatGold}", line);
        Assert.DoesNotContain(CharacterCommandText.GhostMark, line);
        Assert.DoesNotContain('\n', line);
    }

    [Fact]
    public void ListFile_IsAPlainTextDumpNextToCharactersJson()
    {
        Assert.Equal("sosaria-list.txt", CharacterCommandText.ListFileName);
        Assert.EndsWith(CharacterCommandText.ListFileName, CharacterCommands.ListLogPath());
    }

    [Fact]
    public void ListReport_JoinsLinesAndCountForTheTerminal()
    {
        var body = CharacterCommandText.ListReport(["Connor lumber", "Mira mine"], 2);

        Assert.Contains("Connor lumber", body);
        Assert.Contains("Mira mine", body);
        Assert.Contains(CharacterCommandText.CountLine(2), body);
        Assert.Equal(
            "Wrote 2 characters to /tmp/sosaria-list.txt.",
            CharacterCommandText.WroteFile("/tmp/sosaria-list.txt", 2)
        );
    }

    [Fact]
    public void DistanceLabel_MarksOtherFacet()
    {
        Assert.Equal(CharacterCommandText.AnotherFacet, CharacterCommandText.DistanceLabel(false, FortyTiles));
        Assert.Equal($"{FortyTiles}{CharacterCommandText.TilesSuffix}", CharacterCommandText.DistanceLabel(true, FortyTiles));
    }

    [Fact]
    public void WatchLines_ShowLifeState()
    {
        var ambition = new Ambition(
            AmbitionKind.Gold,
            BoatTarget,
            AmbitionRules.DefaultBoatGold,
            GoldProgress
        );
        var opinion = new Bond(ConnorId, StaffId, HealedScore, default, string.Empty, default, NothingShared, HealedReason);
        var memory = new string[CharacterCommandText.WatchMemoryLines + 2];

        for (var i = 0; i < memory.Length; i++)
        {
            memory[i] = $"old{i}";
        }

        memory[^2] = "I banked 80 logs.";
        memory[^1] = "I greeted Aria.";

        var lines = CharacterCommandText.WatchLines(
            ConnorName,
            HuntActivity,
            BritainLocation,
            FeluccaMap,
            ambition,
            AfternoonHour,
            DayPart.Work,
            ActiveStart,
            ActiveEnd,
            opinion,
            memory
        );

        Assert.Contains($"{ConnorName} at {BritainLocation} on {FeluccaMap}", lines);
        Assert.Contains($"{CharacterCommandText.DoingPrefix}{HuntActivity}", lines);
        Assert.Contains(lines, line => line.Contains(BoatTarget) && line.Contains($"{GoldProgress}/{AmbitionRules.DefaultBoatGold}"));
        Assert.Contains(CharacterCommandText.DayLine(AfternoonHour, DayPart.Work, ActiveStart, ActiveEnd), lines);
        Assert.Contains(CharacterCommandText.OpinionLine(opinion), lines);
        Assert.Contains($"{CharacterCommandText.MemoryItemPrefix}I banked 80 logs.", lines);
        Assert.Contains($"{CharacterCommandText.MemoryItemPrefix}I greeted Aria.", lines);
        Assert.DoesNotContain($"{CharacterCommandText.MemoryItemPrefix}old0", lines);
    }

    [Fact]
    public void WatchLines_ShowBackgroundVoiceAndIdle()
    {
        var background = "Born in Britain. Cuts wood for a living.";
        var voice = "Short sentences. Friendly, a little tired.";
        var idle = "Bank's busy this time of day.";
        var lines = CharacterCommandText.WatchLines(
            ConnorName,
            HuntActivity,
            BritainLocation,
            FeluccaMap,
            new Ambition(AmbitionKind.None, "", 0, 0),
            AfternoonHour,
            DayPart.Work,
            ActiveStart,
            ActiveEnd,
            null,
            [],
            background: background,
            voice: voice,
            idleLine: idle
        );

        Assert.Contains($"{CharacterCommandText.BackgroundPrefix}{background}", lines);
        Assert.Contains($"{CharacterCommandText.VoicePrefix}{voice}", lines);
        Assert.Contains($"{CharacterCommandText.IdlePrefix}{idle}", lines);
    }

    [Fact]
    public void DecisionLines_ShowGoalTopThreeAndReason()
    {
        var score = new ScoreResult
        {
            Goal = new Goal(GoalKind.Work, null),
            Ranked =
            [
                new ScoredAction(new ActionId("work:Mine:1"), SkillKinds.Mine, "work", 4.2, "at the work place"),
                new ScoredAction(new ActionId("work:GoTo:bank"), SkillKinds.GoTo, "work", 3.1, "carry goods to town"),
                new ScoredAction(new ActionId("town:IdleWander:0"), SkillKinds.IdleWander, "town", 1.0, "ok")
            ],
            Winner = new ScoredAction(new ActionId("work:Mine:1"), SkillKinds.Mine, "work", 4.2, "at the work place"),
            WinnerReason = "work:Mine:1 because the pack has goods to sell in town"
        };

        var lines = CharacterCommandText.DecisionLines(score);
        Assert.Contains($"{CharacterCommandText.GoalPrefix}Work", lines);
        Assert.Contains(lines, line => line.StartsWith(CharacterCommandText.ChoicesPrefix) && line.Contains("work:Mine:1"));
        Assert.Contains($"{CharacterCommandText.ChosePrefix}{score.WinnerReason}", lines);
    }

    [Fact]
    public void WatchLines_EmptyMemoryAndNoOpinion()
    {
        var lines = CharacterCommandText.WatchLines(
            ConnorName,
            HuntActivity,
            BritainLocation,
            FeluccaMap,
            new Ambition(AmbitionKind.None, "", 0, 0),
            AfternoonHour,
            DayPart.Evening,
            null,
            null,
            null,
            []
        );

        Assert.Contains($"{CharacterCommandText.OpinionPrefix}{CharacterCommandText.NoOpinion}", lines);
        Assert.Contains($"{CharacterCommandText.MemoryPrefix} {CharacterCommandText.NoMemory}", lines);
        Assert.Contains(CharacterCommandText.DispositionLine(DispositionRules.NeutralName), lines);
        Assert.Contains(CharacterCommandText.PartyLine(null), lines);
        Assert.Contains(AmbitionRules.DescribeNone, CharacterCommandText.AmbitionBrief(new Ambition(AmbitionKind.None, "", 0, 0)));
        Assert.Equal(
            $"{CharacterCommandText.DayPrefix}{AfternoonHour} {DayPart.Evening} (active {DayShapeRules.DefaultStartHour}-{DayShapeRules.DefaultEndHour})",
            CharacterCommandText.DayLine(AfternoonHour, DayPart.Evening, null, null)
        );
    }

    [Fact]
    public void LastMemory_KeepsNewestLines()
    {
        var memory = new[] { "a", "b", "c", "d" };
        var last = CharacterCommandText.LastMemory(memory, 2);

        Assert.Equal(["c", "d"], last);
        Assert.Empty(CharacterCommandText.LastMemory([], CharacterCommandText.WatchMemoryLines));
        Assert.Empty(CharacterCommandText.LastMemory(null, CharacterCommandText.WatchMemoryLines));
    }

    [Fact]
    public void TextAfterName_TakesTheSpokenLine()
    {
        Assert.Equal("the boat is nearly paid", CharacterCommandText.TextAfterName("Connor the boat is nearly paid"));
        Assert.Equal("hello", CharacterCommandText.TextAfterName("  Bern   hello  "));
        Assert.Null(CharacterCommandText.TextAfterName("Connor"));
        Assert.Null(CharacterCommandText.TextAfterName(""));
        Assert.Null(CharacterCommandText.TextAfterName(null));
    }

    [Fact]
    public void PlanLines_ShowControllerAndCounts()
    {
        var diag = new PlanDiagnostics();
        diag.NoteFallback("no-model-plan");
        var lines = CharacterCommandText.PlanLines(diag);
        Assert.Contains(lines, line => line.StartsWith("Control: "));
        Assert.Contains(lines, line => line.StartsWith("Counts: "));
        Assert.Contains(lines, line => line.Contains("fallback"));
    }
}
