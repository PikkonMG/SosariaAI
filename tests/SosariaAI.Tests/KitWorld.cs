using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;
using SosariaAI.Tests.RealMap;

namespace SosariaAI.Tests;

/// <summary>
/// Enough of the server for a character to dress through its real first-day path: the
/// craft lists that name makers, the races, the skill slots, the content types a kit names,
/// and the client art a pack drop measures. Tests that use it run in the real-map
/// collection, alone, and skip without the client data.
/// </summary>
internal static class KitWorld
{
    private const uint FirstSerial = 0x7E000;
    private static uint _nextSerial = FirstSerial;
    private static readonly object Gate = new();
    private static bool _ready;
    private static bool _beastsReady;

    /// <summary>Think delays, in seconds, of the engine's medium speed class: a beast needs one to be made.</summary>
    private const double MediumActiveSpeed = 0.2;

    private const double MediumPassiveSpeed = 0.4;

    public static void Ensure()
    {
        lock (Gate)
        {
            if (_ready)
            {
                return;
            }

            _ = RealMapWorld.Felucca;
            TestMap.EnsureInternal();
            TestMap.EnsureRunningWorld();
            Timer.Init(0);
            DefBlacksmithy.Initialize();
            DefTailoring.Initialize();
            DefBowFletching.Initialize();
            DefCarpentry.Initialize();
            DefTinkering.Initialize();

            if (Race.Human == null)
            {
                Server.Misc.RaceDefinitions.Configure();
            }

            // Kit pieces are found by type name in the loaded content, as on the server.
            AssemblyHandler.Assemblies ??= [typeof(Item).Assembly, typeof(Katana).Assembly];

            TestSkills.EnsureTable();
            _ready = true;
        }
    }

    /// <summary>The kit world, and the engine's speed class a beast is made with.</summary>
    public static void EnsureBeasts()
    {
        Ensure();

        lock (Gate)
        {
            if (_beastsReady)
            {
                return;
            }

            NPCSpeeds.RegisterSpeed(
                new NPCSpeeds.SpeedClassEntry
                {
                    Level = SpeedLevel.Medium, ActiveSpeed = MediumActiveSpeed, PassiveSpeed = MediumPassiveSpeed, Types = []
                }
            );
            _beastsReady = true;
        }
    }

    public static PersonProfile Profile(PersonClass personClass, SkillTier tier, PersonWealth wealth, bool female) =>
        new(personClass, tier, PersonTrait.None, wealth, ActivityTendencies.Even, PersonProfile.NeutralPhaseLength, female);

    /// <summary>A fresh copy dressed the way the spawner does it: build, kit, clothes, finish.</summary>
    public static SosariaCharacter Dress(PersonProfile profile, string id)
    {
        var character = Blank(id, profile.Female);
        character.BindProfile(profile);
        character.ApplyFreshBuild();
        CharacterLooks.Apply(character, id, null, profile, alreadyInWorld: false);
        character.CompleteFreshStart();
        return character;
    }

    /// <summary>A copy with no build or clothes yet, standing off the map.</summary>
    public static SosariaCharacter Blank(string id, bool female)
    {
        var character = new SosariaCharacter((Serial)_nextSerial++) { CharacterId = id, Female = female };
        character.DefaultMobileInit();
        return character;
    }
}
