using System;

namespace SosariaAI.Navigation;

/// <summary>
/// A weaponsmith sells blades. It is not a forge. The vendor role "Smith" was applied
/// to any name that contained "smith", so Cove's weaponsmith became the nearest
/// blacksmith and every smithing step failed there.
/// </summary>
public static class ForgeShop
{
    public const string SmithRole = "Smith";
    public const string WeaponsmithToken = "weaponsmith";
    public const string ForgeToken = "forge";

    public static bool IsForgeShop(string name, string role)
    {
        if (Contains(name, WeaponsmithToken) && !Contains(name, ShopFinder.SmithToken))
        {
            return false;
        }

        if (Contains(name, ShopFinder.SmithToken) || Contains(name, ForgeToken))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(role) &&
               role.Equals(SmithRole, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string value, string token) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(token, StringComparison.OrdinalIgnoreCase);
}
