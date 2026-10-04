namespace SosariaAI.Skills;

/// <summary>
/// Who may buy a mount. A riderless mount nearby counts as owned even if the copy
/// walked off without it; the follower cap keeps a full stable from buying more.
/// </summary>
public static class MountBuyRules
{
    public const int VendorSearchRange = 6;
    public const int HorseSlots = 1;

    // The ModernUO animal trainer sells a horse for 550 gold. A buyer short of that walked
    // to the stable, failed at the counter, and walked back to try again.
    public const int HorsePrice = 550;

    public static bool MayBuy(bool mounted, bool ownsMountNearby, int followers, int controlSlots, int followersMax) =>
        !mounted && !ownsMountNearby && followers + controlSlots <= followersMax;

    public static bool CanAfford(int gold) => gold >= HorsePrice;
}
