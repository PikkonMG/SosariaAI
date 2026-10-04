using Server;
using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>
/// What a murderer carries from its first day: a red name, and the trade skills of the era's
/// PKs. A field dexxer trades parry and resist for tracking and hiding; a red mage trades
/// wrestling for hiding. Pure: rolled from the id and the class.
/// </summary>
public static class PkBuildRules
{
    /// <summary>A red starts a few murders past the line, not all on the same count.</summary>
    public const int StartingMurderSpread = 6;

    private const int MurderSalt = 0x5D1;

    private static readonly (SkillName From, SkillName To)[] DexxerTrade =
    [
        (SkillName.Parry, SkillName.Tracking),
        (SkillName.MagicResist, SkillName.Hiding)
    ];

    private static readonly (SkillName From, SkillName To)[] MageTrade =
    [
        (SkillName.Wrestling, SkillName.Hiding)
    ];

    public static int StartingMurders(string characterId) =>
        PkRules.MurdersToRed + PersonDice.Roll(characterId, MurderSalt, StartingMurderSpread);

    /// <summary>
    /// The skill moves that turn a class into its murderer build. A move is made once: the
    /// points go only while the old skill holds more than the new one.
    /// </summary>
    public static (SkillName From, SkillName To)[] TradeSwaps(PersonClass personClass) =>
        personClass switch
        {
            PersonClass.Warrior or PersonClass.Fencer or PersonClass.Archer or PersonClass.Ranger
                or PersonClass.Paladin or PersonClass.Samurai => DexxerTrade,
            PersonClass.Mage or PersonClass.Healer or PersonClass.Bard or PersonClass.Tamer
                or PersonClass.TreasureHunter or PersonClass.Necromancer => MageTrade,
            _ => []
        };

    public static bool ShouldSwap(double fromBase, double toBase) => fromBase > toBase;
}
