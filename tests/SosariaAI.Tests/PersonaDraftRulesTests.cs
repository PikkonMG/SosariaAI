using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class PersonaDraftRulesTests
{
    private const string FencedReply = "```json\n{0}\n```";
    private const string PlainBackground = "Grew up on a farm near Yew. Keeps a small house there.";

    [Fact]
    public void Vet_KeepsAValidDraft_AndTrimsWants()
    {
        var clean = PersonaDraftRules.Vet(PersonaDraftSamples.Valid(), EraBand.T2A, out var reason);

        Assert.Null(reason);
        Assert.NotNull(clean);
        Assert.Equal(PersonaDraftRules.IdleCount, clean.IdleLines.Count);
        Assert.Equal(PersonaDraftRules.GreetingCount, clean.GreetingLines.Count);
        Assert.Equal("save for a small house near Minoc", clean.Wants[0]);
    }

    [Fact]
    public void Parse_ReadsAFencedModelReply()
    {
        var raw = string.Format(FencedReply, PersonaDraftSamples.ModelReply(PersonaDraftSamples.Valid()));
        var draft = PersonaDraftRules.Parse(raw);

        Assert.NotNull(draft);
        Assert.Equal(PersonaDraftRules.IdleCount, draft.IdleLines.Count);
        Assert.Null(PersonaDraftRules.Parse("sorry, i cannot do that"));
        Assert.Null(PersonaDraftRules.Parse("{\"idleLines\": 5}"));
    }

    [Theory]
    [InlineData("i am just a bot")]
    [InlineData("best game ever")]
    [InlineData("check my youtube")]
    public void Vet_RejectsTheWholeDraft_ForTalkFromOutsideTheWorld(string line)
    {
        var draft = PersonaDraftSamples.Valid();
        draft.IdleLines[0] = line;

        Assert.Null(PersonaDraftRules.Vet(draft, EraBand.Modern, out var reason));
        Assert.StartsWith("uses", reason);
    }

    [Fact]
    public void Vet_RejectsLaterEraContent_OnlyInEarlierBands()
    {
        var draft = PersonaDraftSamples.Valid();
        draft.LootLines[0] = "got a paladin sword";

        Assert.Null(PersonaDraftRules.Vet(draft, EraBand.T2A, out _));
        Assert.NotNull(PersonaDraftRules.Vet(draft, EraBand.ML, out _));

        draft.LootLines[0] = "imbuing this later";
        Assert.Null(PersonaDraftRules.Vet(draft, EraBand.ML, out _));
        Assert.NotNull(PersonaDraftRules.Vet(draft, EraBand.Modern, out _));
    }

    [Theory]
    [InlineData("*waves*")]
    [InlineData("/me nods")]
    [InlineData("\"hello there\"")]
    [InlineData("hi — there")]
    [InlineData("hey {other}")]
    public void Vet_DropsEmoteAndMarkupLines_AndFailsWhenTooFewAreLeft(string line)
    {
        var draft = PersonaDraftSamples.Valid();
        draft.IdleLines[0] = line;

        Assert.Null(PersonaDraftRules.Vet(draft, EraBand.Modern, out var reason));
        Assert.Equal("too few idle lines", reason);
        Assert.False(PersonaDraftRules.IsChatLine(line, allowName: false));
    }

    [Fact]
    public void Vet_TakesASpareLine_WhenOneIsBad()
    {
        var draft = PersonaDraftSamples.Valid();
        draft.IdleLines[0] = "*waves*";
        draft.IdleLines.Add("spare line here");

        var clean = PersonaDraftRules.Vet(draft, EraBand.Modern, out _);

        Assert.NotNull(clean);
        Assert.Equal("spare line here", clean.IdleLines[^1]);
    }

    [Fact]
    public void Vet_DropsADuplicateAcrossPools()
    {
        var draft = PersonaDraftSamples.Valid();
        draft.LootLines[0] = "Anyone need iron?";

        Assert.Null(PersonaDraftRules.Vet(draft, EraBand.Modern, out var reason));
        Assert.Equal("too few loot lines", reason);
    }

    [Fact]
    public void Vet_AllowsTheNamePlaceholderOnlyInGreetings()
    {
        Assert.True(PersonaDraftRules.IsChatLine("hail {name}", allowName: true));
        Assert.False(PersonaDraftRules.IsChatLine("hail {name}", allowName: false));
    }

    [Fact]
    public void Vet_CutsAThreeSentenceBackgroundToTwo()
    {
        var draft = PersonaDraftSamples.Valid();
        draft.Background = "Came from Vesper as a child. Mines iron by the hills. Sells it at the bank.";

        var clean = PersonaDraftRules.Vet(draft, EraBand.Modern, out var reason);

        Assert.Null(reason);
        Assert.Equal("Came from Vesper as a child. Mines iron by the hills.", clean.Background);
    }

    [Fact]
    public void FitBackground_CutsALongSentenceOnAWholeWord()
    {
        var longSentence = "Walks " + string.Join(" ", System.Linq.Enumerable.Repeat("the long road", 40)) + " home";

        var fitted = PersonaDraftRules.FitBackground(longSentence);

        var kept = fitted[..^1];

        Assert.True(fitted.Length <= PersonaDraftRules.BackgroundMaxCharacters);
        Assert.EndsWith(".", fitted);
        Assert.StartsWith(kept, longSentence);
        Assert.Equal(' ', longSentence[kept.Length]);
    }

    [Fact]
    public void Vet_RejectsATooShortBackground()
    {
        var draft = PersonaDraftSamples.Valid();
        draft.Background = "Short.";

        Assert.Null(PersonaDraftRules.Vet(draft, EraBand.Modern, out var reason));
        Assert.Equal("bad background", reason);
    }

    [Fact]
    public void Vet_IgnoresABannedWordInTheCutPartOfTheBackground()
    {
        var draft = PersonaDraftSamples.Valid();
        draft.Background = "Came from Vesper as a child. Mines iron by the hills. Plays a game at night.";

        Assert.NotNull(PersonaDraftRules.Vet(draft, EraBand.Modern, out _));
    }

    [Fact]
    public void ToPersona_KeepsTheComposedNumbers_AndPutsWantsInTheBackground()
    {
        var composed = PersonasFile.CreateDefaultConnor();
        var clean = PersonaDraftRules.Vet(PersonaDraftSamples.Valid(), EraBand.Modern, out _);
        var persona = clean.ToPersona(composed);

        Assert.Equal(composed.Id, persona.Id);
        Assert.Equal(composed.Drives, persona.Drives);
        Assert.Equal(composed.Disposition, persona.Disposition);
        Assert.Equal(composed.ActiveStartHour, persona.ActiveStartHour);
        Assert.Equal(clean.IdleLines, persona.IdleLines);
        Assert.Equal(clean.GreetingLines, persona.Greetings);
        Assert.EndsWith(
            PersonaDraft.WantsLead + "save for a small house near Minoc; reach grandmaster mining.",
            persona.Background
        );
        Assert.DoesNotContain("Wants:", persona.Background);
    }

    // The lead says the wants are hopes for a later day. It must add nothing the ambition
    // seed reads: the seed is the same as for the bare wants, and it is what the wants say.
    [Theory]
    [InlineData("a quiet life", "saving for a small boat", AmbitionKind.Gold, "a small boat")]
    [InlineData("hunt the ogres of the hills", "a quiet life", AmbitionKind.Kills, "Ogre")]
    [InlineData("see the shrine at Moonglow", "a quiet life", AmbitionKind.Place, "Moonglow")]
    public void ToPersona_WantsLead_LeavesTheAmbitionSeedToTheWants(
        string firstWant,
        string secondWant,
        AmbitionKind kind,
        string target)
    {
        var draft = new PersonaDraft
        {
            Background = PlainBackground,
            Wants = [firstWant, secondWant]
        };
        var bareWants = PlainBackground + " " + firstWant + PersonaDraft.WantsSeparator + secondWant + PersonaDraft.SentenceEnd;

        var seed = AmbitionRules.SeedFromBackground(draft.ToPersona(null).Background);

        Assert.Equal(kind, seed.Kind);
        Assert.Equal(target, seed.Target);
        Assert.Equal(AmbitionRules.SeedFromBackground(bareWants), seed);
    }

    [Fact]
    public void Vet_DropsPromiseLines_AndTakesTheSpares()
    {
        var draft = PersonaDraftSamples.Valid();
        draft.IdleLines[0] = "heading to minoc, u coming or what";
        draft.IdleLines[1] = "anyone wanna hunt orcs";
        draft.IdleLines.AddRange(["spare line here", "another spare heh"]);
        draft.GreetingLines[0] = "hail {name}, you heading to brit too";
        draft.GreetingLines.Add("well met {name}");
        draft.ReturnLines[0] = "back, meet me at the bank";
        draft.ReturnLines.Add("lost it all again");

        var clean = PersonaDraftRules.Vet(draft, EraBand.T2A, out var reason);

        Assert.Null(reason);
        Assert.Equal(PersonaDraftRules.IdleCount, clean.IdleLines.Count);
        Assert.Equal(PersonaDraftRules.GreetingCount, clean.GreetingLines.Count);
        Assert.Equal(PersonaDraftRules.ReturnCount, clean.ReturnLines.Count);
        Assert.Contains("another spare heh", clean.IdleLines);
        Assert.Contains("well met {name}", clean.GreetingLines);
        Assert.Contains("lost it all again", clean.ReturnLines);
        var kept = new List<string>(clean.IdleLines);
        kept.AddRange(clean.GreetingLines);
        kept.AddRange(clean.ReturnLines);
        Assert.All(kept, line => Assert.False(PromiseLines.IsPromise(line), line));
    }

    [Fact]
    public void Vet_FailsShort_WhenAPromiseLineHasNoSpare()
    {
        var draft = PersonaDraftSamples.Valid();
        draft.CombatLines[0] = "orcs here, follow me";

        Assert.Null(PersonaDraftRules.Vet(draft, EraBand.T2A, out var reason));
        Assert.Equal("too few combat lines", reason);
    }

    [Fact]
    public void EraWords_MatchWholeWordsAndPlurals()
    {
        Assert.Equal("paladin", PersonaEraWords.FindBanned("two Paladins ran by", EraBand.T2A));
        Assert.Null(PersonaEraWords.FindBanned("i said hi to the aide", EraBand.T2A));
        Assert.Null(PersonaEraWords.FindBanned("my plate gauntlets are worn", EraBand.T2A));
        Assert.Empty(PersonaEraWords.NotYetIn(EraBand.Modern));
    }
}
