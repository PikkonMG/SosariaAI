using Server;
using Server.Mobiles;
using SosariaAI.Mobiles;

namespace SosariaAI.Memory;

/// <summary>
/// A person long-term memory can name: a SosariaAI character or a real player. The id never
/// uses the name, so a name change does not break memories.
/// </summary>
public readonly record struct PersonRef(string Id, string Name, bool IsBot)
{
    /// <summary>Id prefix of a SosariaAI character; the character id follows.</summary>
    public const string BotPrefix = "bot:";

    /// <summary>Id prefix of a real player; the mobile serial follows.</summary>
    public const string PlayerPrefix = "player:";

    /// <summary>A SosariaAI character by its character id.</summary>
    public static PersonRef Bot(string characterId, string name) => new(BotPrefix + characterId, name ?? string.Empty, true);

    /// <summary>A real player by its mobile serial.</summary>
    public static PersonRef Player(Serial serial, string name) => new(PlayerPrefix + serial, name ?? string.Empty, false);

    /// <summary>
    /// The person behind a mobile: a SosariaAI character with a character id, or a real player.
    /// Null for creatures, other non-player mobiles, and deleted or missing mobiles.
    /// </summary>
    public static PersonRef? Of(Mobile mobile)
    {
        if (mobile == null || mobile.Deleted)
        {
            return null;
        }

        if (mobile is SosariaCharacter character)
        {
            return string.IsNullOrWhiteSpace(character.CharacterId) ? null : Bot(character.CharacterId, character.Name);
        }

        return mobile is PlayerMobile ? Player(mobile.Serial, mobile.Name) : null;
    }
}
