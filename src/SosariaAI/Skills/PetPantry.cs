using System;
using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// A tamer's shopping for pet food (<see cref="PetFoodRules"/>): a pet it keeps with fewer than
/// <see cref="PetFoodRules.LowMark"/> units in the pack of what it eats sends the tamer to a
/// shop that sells that food, to buy up to <see cref="PetFoodRules.PantryTarget"/>. Tamers that
/// never bought food fed their pets thirteen times in a night, and pets whose food ran out went
/// wild. The plan is one errand of the shopping trip (<see cref="VendorBuySkill"/>).
/// </summary>
public static class PetPantry
{
    /// <summary>
    /// True when a pet the person keeps is short of a food a shop sells. A tamer short of pet
    /// food goes shopping.
    /// </summary>
    public static bool IsLow(SosariaCharacter character) => PetFoodRules.Needs(Larders(character)).Count > 0;

    /// <summary>
    /// Fills <paramref name="wanted"/> and returns the shop to walk to, or null. The hungriest
    /// pet picks the shop: the nearest stocked shop in reach selling the first food it eats that
    /// the person can pay one unit of. Every other short pet whose food that vendor sells joins
    /// the list.
    /// </summary>
    public static StockedShop? Plan(SosariaCharacter character, List<(Type Type, int Amount)> wanted)
    {
        var needs = PetFoodRules.Needs(Larders(character));

        for (var n = 0; n < needs.Count; n++)
        {
            var foods = needs[n].Foods;

            for (var f = 0; f < foods.Count; f++)
            {
                if (ShopFinder.NearestStocked(character, foods[f].Shops, [foods[f].Item]) is { } shop &&
                    PaysForOne(character, shop.Vendor, foods[f].Item))
                {
                    AddSold(character, shop.Vendor, needs, wanted);
                    return shop;
                }
            }
        }

        return null;
    }

    private static List<PetLarder> Larders(SosariaCharacter character) =>
        PetKeeper.KeptPets(character).ConvertAll(pet => new PetLarder(pet.FavoriteFood, PetKeeper.FoodOnHand(character, pet)));

    // Each short pet takes the first food it eats that the vendor stocks and the person can pay for.
    private static void AddSold(
        SosariaCharacter character,
        BaseVendor vendor,
        List<PantryNeed> needs,
        List<(Type Type, int Amount)> wanted
    )
    {
        for (var n = 0; n < needs.Count; n++)
        {
            var foods = needs[n].Foods;

            for (var f = 0; f < foods.Count; f++)
            {
                if (PaysForOne(character, vendor, foods[f].Item))
                {
                    PetFoodRules.AddLine(wanted, foods[f].Item, needs[n].Shortfall);
                    break;
                }
            }
        }
    }

    // The vendor stocks the food and the person can pay its price for one unit.
    private static bool PaysForOne(SosariaCharacter character, BaseVendor vendor, Type item) =>
        VendorBuySkill.PaysForOne(
            VendorDeal.CheapestPrice([vendor], [item]),
            character.Backpack?.GetAmount(typeof(Gold)) ?? 0,
            Banker.GetBalance(character)
        );
}
