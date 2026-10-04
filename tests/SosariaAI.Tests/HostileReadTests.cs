using Server;
using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Navigation.Generation;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using SosariaAI.Tests.RealMap;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// The fight reads a live foe as the floor ratings read its kind: a creature's own blow and its
/// arts, the spells of a mage's brain, a breath, the poison on its blows and a bow. The fight read
/// the blows alone, and fighters a floor fitted opened on the dread spiders that made its rating.
/// The test world registers no poisons, so the bite is left to the threat math's own tests.
/// </summary>
[Collection(RealMapCollection.Name)]
public class HostileReadTests
{
    /// <summary>Open ground west of Britain, clear of the guards.</summary>
    private static readonly Point3D Wilds = new(1122, 957, 0);

    private static readonly Point3D BesideFighter = new(1123, 957, 0);

    public HostileReadTests()
    {
        if (RealMapWorld.Available)
        {
            KitWorld.EnsureBeasts();
        }
    }

    [RealMapFact]
    public void Of_ACreature_CountsItsOwnBlowAndItsArts()
    {
        var spider = new DreadSpider();
        var dragon = new Dragon();
        var archer = new RatmanArcher();
        var orc = new Orc();

        try
        {
            var read = HostileRead.Of(spider);

            Assert.Equal(new HostileStats(spider.Hits, spider.Str, (spider.DamageMin + spider.DamageMax) / 2, read.Arts), read);
            Assert.Equal((int)spider.Skills.Magery.Value, read.Arts.SpellSkill);
            Assert.True(HostileRead.Of(dragon).Arts.Breath);
            Assert.True(HostileRead.Of(archer).Arts.Ranged);
            Assert.Equal(default, HostileRead.Of(orc).Arts);
            Assert.True(ThreatRating.Score([read]) > ThreatRating.Score([read with { Arts = default }]));
        }
        finally
        {
            spider.Delete();
            dragon.Delete();
            archer.Delete();
            orc.Delete();
        }
    }

    /// <summary>The floor ratings read a kind's arts as the fight reads one of its kind (its Magery is the mean of a few).</summary>
    [RealMapFact]
    public void Of_ACreature_ReadsTheArtsTheFloorRatingsRead()
    {
        var spider = new DreadSpider();

        try
        {
            var live = HostileRead.Of(spider).Arts;
            var kind = CreatureStatsLookup.TryLive(nameof(DreadSpider))!.Value.Arts;

            Assert.Equal(live with { SpellSkill = kind.SpellSkill }, kind);
            Assert.True(kind.SpellSkill > 0);
        }
        finally
        {
            spider.Delete();
        }
    }

    [RealMapFact]
    public void Of_APerson_CountsTheHuntersBlowAndNoArts()
    {
        var person = KitWorld.Blank("Felucca:hostile#1", female: false);

        try
        {
            Assert.Equal(new HostileStats(person.Hits, person.Str, HuntSkill.HuntThreatAverageDamage), HostileRead.Of(person));
        }
        finally
        {
            person.Delete();
        }
    }

    /// <summary>A fighter's sight scan rates a dread spider beside it by its spells and bite too.</summary>
    [RealMapFact]
    public void SightThreat_CountsTheArtsOfAFoeInSight()
    {
        var fighter = KitWorld.Dress(KitWorld.Profile(PersonClass.Warrior, SkillTier.Master, PersonWealth.Comfortable, false), "Felucca:hostile#2");
        var spider = new DreadSpider();

        try
        {
            fighter.MoveToWorld(Wilds, RealMapWorld.Felucca);
            spider.MoveToWorld(BesideFighter, RealMapWorld.Felucca);
            var read = HostileRead.Of(spider);

            Assert.Equal(ThreatRating.Score([read]), HuntSkill.SightThreat(fighter));
            Assert.True(HuntSkill.SightThreat(fighter) > ThreatRating.Score([read with { Arts = default }]));
        }
        finally
        {
            spider.Delete();
            fighter.Delete();
        }
    }
}
