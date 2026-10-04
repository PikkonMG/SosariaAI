using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class ActionPlaceRulesTests
{
    [Fact]
    public void IsAt_InsideHarvestRectangle()
    {
        var step = new SkillStepDefinition
        {
            Skill = SkillKinds.Mine,
            Area = new AreaDefinition { X = 1450, Y = 1500, Width = 50, Height = 50 }
        };

        Assert.True(ActionPlaceRules.IsAt(step, new Point3D(1460, 1510, 40), ActionPlaceRules.AtPlaceRange));
        Assert.False(ActionPlaceRules.IsAt(step, new Point3D(1441, 1574, 30), 1));
    }

    [Fact]
    public void IsAt_BankSpotWithinRange()
    {
        var step = new SkillStepDefinition
        {
            Skill = SkillKinds.BankDeposit,
            BankSpot = CharactersFile.DefaultBankSpot
        };

        Assert.True(ActionPlaceRules.IsAt(step, CharactersFile.DefaultBankSpot, CharactersFile.DefaultGoToRange));
        Assert.False(ActionPlaceRules.IsAt(step, new Point3D(1, 1, 0), CharactersFile.DefaultGoToRange));
    }

    [Fact]
    public void IsTownWalk_BankAndSell()
    {
        Assert.True(ActionPlaceRules.IsTownWalk(new SkillStepDefinition { Skill = SkillKinds.VendorSell }));
        Assert.True(
            ActionPlaceRules.IsTownWalk(
                new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = CharactersFile.DefaultBankSpot }
            )
        );
        Assert.False(
            ActionPlaceRules.IsTownWalk(
                new SkillStepDefinition { Skill = SkillKinds.GoTo, Target = CharactersFile.MiraMineApproach }
            )
        );
    }

    [Fact]
    public void AreaGraveyard_DoesNotContainTheMineHill()
    {
        var graveyard = new AreaDefinition
        {
            X = CharactersFile.GraveyardAreaX,
            Y = CharactersFile.GraveyardAreaY,
            Width = CharactersFile.GraveyardAreaWidth,
            Height = CharactersFile.GraveyardAreaHeight
        };

        Assert.False(graveyard.ToRectangle().Contains(new Point3D(1441, 1574, 30)));
        Assert.True(graveyard.ToRectangle().Contains(CharactersFile.GraveyardGoPoint));
    }
}
