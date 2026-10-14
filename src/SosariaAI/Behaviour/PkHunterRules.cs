using SosariaAI.Combat;
using SosariaAI.Spawning;

namespace SosariaAI.Behaviour;

/// <summary>
/// The anti-PKs of 1999: a few solid blue fighters who logged in to hunt reds. They rode on
/// word of a murder or a red seen nearby, now and then on a standing patrol of the hot spots,
/// and now and then into Buccaneer's Den itself; the rest of their time went to the dungeons. Only a few ride into the Den
/// at once, so the reds' town sees a hunter here and there, not a crowd. Who hunts is rolled
/// once per person, so a hunter stays one across restarts. Pure.
/// </summary>
public static class PkHunterRules
{
    /// <summary>A hunter is a fighter of this tier or better: the reds' match, not their prey.</summary>
    public const SkillTier MinTier = SkillTier.Adept;

    /// <summary>The share of blue fighters at <see cref="MinTier"/> or better who hunt reds.</summary>
    public const int HunterPercent = 25;

    /// <summary>The share of a hunter's runs that ride into Buccaneer's Den.</summary>
    public const double DenShare = 0.25;

    /// <summary>At most this many hunters ride in the Den at once, shard-wide.</summary>
    public const int MaxInDen = 2;

    private const int HunterSalt = 461;
    private const int DenSalt = 467;

    /// <summary>The person's own dice make it a hunter (<see cref="HunterPercent"/>), if it can be one.</summary>
    public static bool RollsHunter(string uniqueId) => PersonDice.Chance(uniqueId, HunterSalt, HunterPercent);

    /// <summary>
    /// A blue fighter of <see cref="MinTier"/> or better whose dice make it a hunter
    /// (<see cref="RollsHunter"/>). A red is never one.
    /// </summary>
    public static bool IsHunter(bool rollsHunter, bool fighter, bool red, SkillTier tier) =>
        rollsHunter && fighter && !red && tier >= MinTier;


    /// <summary>
    /// This run rides into the Den (<see cref="DenShare"/>, from the run's roll) while fewer than
    /// <see cref="MaxInDen"/> hunters ride there already.
    /// </summary>
    public static bool RidesToDen(int seed, int huntersInDen) =>
        huntersInDen < MaxInDen && ChoiceSeed.Unit(seed, DenSalt) < DenShare;
}
