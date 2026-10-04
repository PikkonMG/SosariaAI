using Server;
using Server.Items;

namespace SosariaAI.Skills;

/// <summary>
/// Classic musicianship, the parent of Peace, Discord and Provoke: the packed instrument and
/// the reach every bard song shares. Music practice itself is a <see cref="PracticeSkillTable"/> kind.
/// </summary>
public static class MusicRules
{
    /// <summary>Tiles a bard song reaches with no skill.</summary>
    public const int BaseReachTiles = 8;

    /// <summary>Song skill points for each tile of reach past the base.</summary>
    public const int BardRangeSkillDivisor = 15;

    /// <summary>The reach of a bard song played with <paramref name="songSkill"/>.</summary>
    public static int BardRange(double songSkill) =>
        BaseReachTiles + (int)(songSkill / BardRangeSkillDivisor);

    public static BaseInstrument FindInstrument(Mobile bard) =>
        bard?.Backpack?.FindItemByType<BaseInstrument>();
}
