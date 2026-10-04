namespace SosariaAI.Navigation;

public static class NavLimits
{
    /// <summary>
    /// ModernUO BitmapAStar AreaSize. A hop whose Chebyshev span exceeds this cannot path.
    /// </summary>
    public const int MaxLegDistance = 38;

    /// <summary>
    /// Author target. Keep street legs at or under this so a wall detour still fits the box.
    /// </summary>
    public const int SoftLegDistance = 32;

    public const int SameFloorMaxDeltaZ = 10;
    public const int FloorPenaltyPerZ = 3;
    public const int DefaultArrivalRange = 3;
    public const int BankArrivalRange = 6;

    /// <summary>
    /// Vendor keepers stand behind a counter or bar; tile-adjacent arrival can never
    /// complete. Counter reach is what the trade needs — the same span a bank visit uses.
    /// </summary>
    public const int ShopArrivalRange = 6;
    public const int DoorArrivalRange = 1;
}
