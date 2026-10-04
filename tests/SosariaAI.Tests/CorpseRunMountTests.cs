using Server;
using Server.Mobiles;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Tests.RealMap;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// The engine sets a dead rider's mount down at the body, and the mount follows the ghost and
/// then the raised person. The raised rider gets on first, then runs to its body: the remount
/// waited out the whole run, bank and shops too, with the horse trailing behind.
/// </summary>
[Collection(RealMapCollection.Name)]
public class CorpseRunMountTests
{
    private static readonly Point3D BritainStreet = new(1415, 1690, 0);
    private static readonly Point3D BesideRider = new(1416, 1690, 0);

    public CorpseRunMountTests()
    {
        if (RealMapWorld.Available)
        {
            KitWorld.EnsureBeasts();
        }
    }

    [RealMapFact]
    public void RaisedRider_GetsOnItsOwnMountBeforeTheRun()
    {
        var (rider, horse) = RiderBesideItsHorse("Felucca:corpse-run-mount#1");

        try
        {
            var run = new CorpseRunSkill();

            Assert.True(run.Begin(rider));
            Assert.Equal(SkillStatus.Running, run.Tick());
            Assert.False(rider.Mounted);

            run.Tick();

            Assert.True(rider.Mounted);
            Assert.Equal(rider, horse.Rider);
        }
        finally
        {
            horse.Delete();
            rider.Delete();
        }
    }

    [RealMapFact]
    public void RaisedRider_InAFight_DoesNotStopToMount()
    {
        var (rider, horse) = RiderBesideItsHorse("Felucca:corpse-run-mount#2");
        var foe = KitWorld.Blank("Felucca:corpse-run-mount#3", female: false);
        foe.MoveToWorld(BritainStreet, RealMapWorld.Felucca);

        try
        {
            rider.Combatant = foe;

            var run = new CorpseRunSkill();
            Assert.True(run.Begin(rider));
            run.Tick();
            rider.Combatant = null;
            run.Tick();

            Assert.False(rider.Mounted);
            Assert.True(rider.MayRemount());
        }
        finally
        {
            foe.Delete();
            horse.Delete();
            rider.Delete();
        }
    }

    private static (SosariaCharacter Rider, Horse Horse) RiderBesideItsHorse(string id)
    {
        var rider = KitWorld.Blank(id, female: false);
        rider.MoveToWorld(BritainStreet, RealMapWorld.Felucca);
        var horse = new Horse();
        Assert.True(horse.SetControlMaster(rider));
        horse.MoveToWorld(BesideRider, RealMapWorld.Felucca);
        return (rider, horse);
    }
}
