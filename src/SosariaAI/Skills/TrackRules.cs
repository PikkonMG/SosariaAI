namespace SosariaAI.Skills;

/// <summary>
/// T2A tracking: skill check, then notice a nearby mobile in tracking range.
/// Range is 10 tiles plus 10 per 10 skill, matching ModernUO TrackWhoGump.
/// </summary>
public static class TrackRules
{
    public const int BaseRangeTiles = 10;
    public const int RangePerTenSkill = 10;
    public const int TenSkill = 10;
    public const double PracticeMin = 0;
    public const double FirstCheckMax = 21.1;
    public const double SecondCheckMin = 21.1;
    public const double PracticeMax = 100;

    public static int RangeFor(double tracking) =>
        BaseRangeTiles + (int)tracking / TenSkill * RangePerTenSkill;
}
