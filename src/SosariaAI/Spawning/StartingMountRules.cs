using SosariaAI.Combat;

namespace SosariaAI.Spawning;

/// <summary>The ride a person arrives on. Every kind is a Second Age mount.</summary>
public enum StartingMount
{
    None,
    Horse,
    ForestOstard,
    DesertOstard,
    Llama
}

/// <summary>
/// A veteran with savings already owned a mount when it first walked in; a fresh
/// character walked. Horses were the common ride; forest and desert ostards and
/// llamas were the rarer ones. A later mount is bought at a stable through
/// <c>BuyMountSkill</c>; this only covers the one a person arrives with.
/// </summary>
public static class StartingMountRules
{
    public const int RichRiderPercent = 85;
    public const int ComfortableRiderPercent = 50;
    public const int ModestRiderPercent = 15;
    public const int HorseWeight = 70;
    public const int ForestOstardWeight = 12;
    public const int DesertOstardWeight = 10;
    public const int LlamaWeight = 8;

    private const int RideSalt = 1001;
    private const int KindSalt = 1009;

    public static StartingMount Pick(PersonProfile profile, string uniqueId)
    {
        if (profile == null || profile.Tier < SkillTier.Journeyman)
        {
            return StartingMount.None;
        }

        var percent = profile.Wealth switch
        {
            PersonWealth.Rich => RichRiderPercent,
            PersonWealth.Comfortable => ComfortableRiderPercent,
            PersonWealth.Modest => ModestRiderPercent,
            _ => 0
        };

        if (!PersonDice.Chance(uniqueId, RideSalt, percent))
        {
            return StartingMount.None;
        }

        return PersonDice.Weighted(uniqueId, KindSalt, HorseWeight, ForestOstardWeight, DesertOstardWeight, LlamaWeight) switch
        {
            0 => StartingMount.Horse,
            1 => StartingMount.ForestOstard,
            2 => StartingMount.DesertOstard,
            _ => StartingMount.Llama
        };
    }
}
