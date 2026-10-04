using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class DecideChoiceTests
{
    private static readonly ChoiceDefinition[] Choices =
    [
        new() { Routine = "graveyard", Weight = 4 },
        new() { Routine = "town", Weight = 1 }
    ];

    [Fact]
    public void Resolve_KnownId_ReturnsIt() =>
        Assert.Equal("town", DecideChoice.Resolve("town", Choices));

    [Fact]
    public void Resolve_UnknownOrMissing_ReturnsFirst()
    {
        Assert.Equal("graveyard", DecideChoice.Resolve("nope", Choices));
        Assert.Equal("graveyard", DecideChoice.Resolve(null, Choices));
        Assert.Equal(DecideChoice.DefaultRoutineId, DecideChoice.Resolve("x", null));
    }

    [Fact]
    public void Fallback_FixedSeedIsDeterministic()
    {
        Assert.Equal("graveyard", DecideChoice.Fallback(Choices, _ => 0));
        Assert.Equal("town", DecideChoice.Fallback(Choices, _ => 4));
    }

    [Fact]
    public void ParseDecide_ReadsChooseField()
    {
        var (_, choose, _) = ReplyParser.ParseDecide("""{"choose":"graveyard","say":"To the graves.","mood":"grim"}""", 160);
        Assert.Equal("graveyard", choose);
    }

    [Fact]
    public void ParseDecide_MissingChoose_IsEmpty()
    {
        var (say, choose, _) = ReplyParser.ParseDecide("""{"say":"Aye."}""", 160);
        Assert.Equal("Aye.", say);
        Assert.Equal(string.Empty, choose);
    }

    private const string GraveyardRoutine = "graveyard";
    private const string DespiseRoutine = "despise";
    private const int GraveyardPower = 40;
    private const int DespisePower = 120;

    private static ChoiceDefinition[] PoweredChoices() =>
    [
        new() { Routine = GraveyardRoutine, Weight = 3, RequiredPower = GraveyardPower },
        new() { Routine = DespiseRoutine, Weight = 1, RequiredPower = DespisePower }
    ];

    [Fact]
    public void ResolveWithinPower_AllowsContentTheCharacterCanSurvive()
    {
        Assert.Equal(DespiseRoutine, DecideChoice.ResolveWithinPower(DespiseRoutine, PoweredChoices(), DespisePower));
        Assert.Equal(GraveyardRoutine, DecideChoice.ResolveWithinPower(GraveyardRoutine, PoweredChoices(), GraveyardPower));
    }

    [Fact]
    public void ResolveWithinPower_RefusesContentAboveTheCharacter()
    {
        // A veteran at 117 was sent into Despise by the model on the live shard.
        Assert.Null(DecideChoice.ResolveWithinPower(DespiseRoutine, PoweredChoices(), DespisePower - 3));
        Assert.Null(DecideChoice.ResolveWithinPower(GraveyardRoutine, PoweredChoices(), GraveyardPower - 1));
    }

    [Fact]
    public void ResolveWithinPower_ReturnsNullForAnythingItCannotMatch()
    {
        Assert.Null(DecideChoice.ResolveWithinPower("nonsense", PoweredChoices(), DespisePower));
        Assert.Null(DecideChoice.ResolveWithinPower(null, PoweredChoices(), DespisePower));
        Assert.Null(DecideChoice.ResolveWithinPower(DespiseRoutine, [], DespisePower));
    }

    [Fact]
    public void MeetsPower_TreatsNoRequirementAsOpenToAnyone()
    {
        Assert.True(DecideChoice.MeetsPower(new ChoiceDefinition { Routine = "town" }, 0));
        Assert.True(DecideChoice.MeetsPower(null, 0));
    }

    [Fact]
    public void RequiredPowerOf_AuthoredValue_WinsOverCatalog()
    {
        var catalog = new DestinationCatalog(
            [
                new Destination
                {
                    Name = "Despise Entrance",
                    Kind = "Dungeon",
                    Role = "Despise",
                    Node = "hub",
                    Aliases = ["despise"],
                    Difficulty = DespisePower + 80
                }
            ]
        );
        var choice = new ChoiceDefinition { Routine = DespiseRoutine, RequiredPower = DespisePower };

        Assert.Equal(DespisePower, DecideChoice.RequiredPowerOf(choice, catalog));
        Assert.True(DecideChoice.MeetsPower(choice, DespisePower, catalog));
        Assert.Equal(DespiseRoutine, DecideChoice.ResolveWithinPower(DespiseRoutine, [choice], DespisePower, catalog));
    }

    [Fact]
    public void CanonicalChoose_GoHunt_MapsToGraveyard()
    {
        Assert.Equal(
            GraveyardRoutine,
            DecideChoice.CanonicalChoose("go_hunt", PoweredChoices())
        );
    }

    [Fact]
    public void EffectivePower_AddsAlliesOnPartyContent()
    {
        Assert.Equal(40, DecideChoice.EffectivePower(40, 100, partyContent: false));
        Assert.True(DecideChoice.EffectivePower(25, 80, partyContent: true) > 25);
    }

    [Fact]
    public void RequiredPowerOf_MissingCatalog_UsesChoice()
    {
        var choice = new ChoiceDefinition { Routine = DespiseRoutine, RequiredPower = DespisePower };
        Assert.Equal(DespisePower, DecideChoice.RequiredPowerOf(choice, null));
        Assert.Equal(DespisePower, DecideChoice.RequiredPowerOf(choice, new DestinationCatalog([])));
    }
}
