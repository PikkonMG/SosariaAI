using Server;
using Server.Mobiles;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;

namespace SosariaAI.Social;

/// <summary>
/// The town's NPC Thieves Guild. The engine lets only its members lift from other players,
/// and the guards take no murder report of a member's death. Most thieves join it.
/// </summary>
public static class ThievesGuild
{
    public const int EnrollPercent = 85;
    private const int EnrollSalt = 0x7A1;

    public static bool Enrolls(string characterId, bool thief) =>
        thief && PersonDice.Chance(characterId, EnrollSalt, EnrollPercent);

    /// <summary>Signs a thief up once. A member keeps the join date it already has.</summary>
    public static void Enroll(SosariaCharacter character, bool thief)
    {
        if (character == null || character.NpcGuild == NpcGuild.ThievesGuild ||
            !Enrolls(character.CharacterId, thief))
        {
            return;
        }

        character.NpcGuild = NpcGuild.ThievesGuild;
        character.NpcGuildJoinTime = Core.Now;
    }
}
