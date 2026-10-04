using Server;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// The names of the rule clocks a person keeps on itself (<see cref="Mobiles.SosariaCharacter.ClockAt"/>):
/// when it last did a thing that a rule rests it after, or since when a thing has gone on. The
/// save keeps each clock as an absolute UTC time, so a restart neither ends nor lengthens a rest.
/// </summary>
public static class RuleClock
{
    /// <summary>The last Den raid this fighter rode out on, as leader or mate (<see cref="PartyRoadRules.DenRaidRest"/>).</summary>
    public const string DenRaid = "den raid";

    /// <summary>The last war band, sweep or Den raid this fighter led out (<see cref="FactionRules.PatrolRest"/>).</summary>
    public const string BandLed = "band led";

    /// <summary>The close of this tamer's last practice session (<see cref="TameRules.PracticeRest"/>).</summary>
    public const string TamingSession = "taming session";

    /// <summary>When this tamer last stabled its pets to free its slots for taming (<see cref="StableRules.TamingHold"/>).</summary>
    public const string StabledForTaming = "stabled for taming";

    /// <summary>When this tamer last stabled a hurt pet to rest (<see cref="StableRules.PetRestMax"/>).</summary>
    public const string StabledToRest = "stabled to rest";

    /// <summary>When this tamer last claimed its pets from the stables (<see cref="StableRules.RestableGap"/>).</summary>
    public const string PetsClaimed = "pets claimed";

    /// <summary>The end of this fighter's last duel (<see cref="DuelRules.Rest"/>).</summary>
    public const string DuelFought = "duel";

    /// <summary>The start of this fighter's last town scuffle (<see cref="Configuration.TownScuffleSettings.FighterRestMinutes"/>).</summary>
    public const string TownScuffle = "town scuffle";

    /// <summary>When this red last found no shop out of the guards' reach for a piece (<see cref="Combat.SpareKitRules.ShopMissRest"/>).</summary>
    public const string RedShopMissed = "red shop missed";

    /// <summary>This leader's last group call, or the end of its last group (<see cref="LfgRules.ShoutRest"/>).</summary>
    public const string LfgShout = "lfg shout";

    /// <summary>The start of the name of every lost-pet clock.</summary>
    public const string LostPetPrefix = "lost pet ";

    /// <summary>Since when this pet of the tamer's has been lost (<see cref="PetRules.LostPetRelease"/>).</summary>
    public static string LostPet(Serial pet) => $"{LostPetPrefix}{pet.Value}";

    /// <summary>The start of the name of every given-up town trip clock.</summary>
    private const string TownTripPrefix = "town trip given up ";

    /// <summary>
    /// When this person last gave up its town trip to this bank for danger on the road
    /// (<see cref="TownTripRules.GivenUpRest"/>).
    /// </summary>
    public static string TownTripGivenUp(Point3D bank) => $"{TownTripPrefix}{bank}";

    /// <summary>The start of the name of every too-hard floor clock.</summary>
    private const string FloorTooHardPrefix = "floor too hard ";

    /// <summary>
    /// When this person last lost on this level of this dungeon and called it too hard
    /// (<see cref="Skills.DungeonCrawlRules.TooHardRest"/>).
    /// </summary>
    public static string FloorTooHard(string dungeon, int level) => $"{FloorTooHardPrefix}{dungeon} {level}";
}
