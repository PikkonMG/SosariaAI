using System;
using Server;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using SosariaAI.Spawning;
using SosariaAI.Tests.RealMap;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A tamer looks after its pets between two steps, not only while it stands about: the scorer
/// commits the next step in the tick the last one ends, and no tamer ever walked to the stables.
/// </summary>
[Collection(RealMapCollection.Name)]
public class PetErrandTests
{
    public PetErrandTests()
    {
        if (RealMapWorld.Available)
        {
            KitWorld.EnsureBeasts();
        }
    }

    /// <summary>Open ground west of Britain where a tamer took a brown bear in the live run.</summary>
    private static readonly Point3D Wilds = new(1122, 957, 0);

    /// <summary>Past the reach of the tamer's orders (<see cref="PetRules.PetScanRange"/>).</summary>
    private static readonly Point3D LeftBehind = new(1122 + PetRules.PetScanRange + 6, 957, 0);

    private static SosariaCharacter Tamer(string id)
    {
        var tamer = KitWorld.Dress(KitWorld.Profile(PersonClass.Tamer, SkillTier.Master, PersonWealth.Comfortable, false), id);
        tamer.MoveToWorld(Wilds, RealMapWorld.Felucca);
        return tamer;
    }

    [RealMapFact]
    public void TamerBetweenSteps_WalksBackForALostPet_Once()
    {
        var tamer = Tamer("Felucca:osric#32");
        var drake = new Drake();

        try
        {
            drake.MoveToWorld(LeftBehind, RealMapWorld.Felucca);
            Assert.True(drake.SetControlMaster(tamer));

            Assert.True(PetKeeper.StartsStableErrand(tamer));
            Assert.IsType<TravelSkill>(tamer.Routine.CurrentSkill);

            // On its way back it starts nothing else, and the pet is still its own.
            Assert.False(PetKeeper.StartsStableErrand(tamer));
            Assert.True(drake.Controlled);
        }
        finally
        {
            drake.Delete();
            tamer.Delete();
        }
    }
}
