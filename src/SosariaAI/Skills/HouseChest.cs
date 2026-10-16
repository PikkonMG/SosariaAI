using System;
using Server;
using Server.Items;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// What a character puts into a house chest and takes out of it. Inside a house it takes the
/// supplies it runs short of, and an owner at home puts away the pieces it would carry to the
/// bank, as a player kept the overflow of its bank box in chests at home.
/// </summary>
public static class HouseChest
{
    /// <summary>A character takes up to this many heal potions from the chest at a time.</summary>
    public const int PotionsTaken = 3;

    /// <summary>How many a character takes: what it wants, as far as the chest holds.</summary>
    public static int Take(int want, int inChest) => Math.Max(0, Math.Min(want, inChest));

    /// <summary>How many to add to bring a stock up to its mark.</summary>
    public static int TopUp(int have, int target) => Math.Max(0, target - Math.Max(0, have));

    public static Item Stack(Type type, int amount) => (Item)Activator.CreateInstance(type, amount);

    /// <summary>
    /// Takes what the character runs short of from the chest: bandages and reagents up to its
    /// own marks, and a few heal potions. The number of things taken.
    /// </summary>
    public static int Restock(SosariaCharacter character, Container chest)
    {
        if (chest is not { Deleted: false } || character.Backpack is not { } pack)
        {
            return 0;
        }

        var taken = 0;

        foreach (var need in SupplyCheck.LowNeeds(character))
        {
            if (need.Kind is not (SupplyKind.Bandages or SupplyKind.Reagents or SupplyKind.TravelReagents))
            {
                continue;
            }

            foreach (var (type, amount) in SupplyCheck.BuyLines(pack, need))
            {
                var take = Take(amount, chest.GetAmount(type));

                if (take > 0 && chest.ConsumeUpTo(type, take) == take)
                {
                    pack.DropItem(Stack(type, take));
                    taken += take;
                }
            }
        }

        var potions = Take(
            TopUp(pack.GetAmount(typeof(GreaterHealPotion)), PotionsTaken),
            chest.GetAmount(typeof(GreaterHealPotion))
        );

        for (var i = 0; i < potions && chest.FindItemByType<GreaterHealPotion>() is { } potion; i++)
        {
            pack.DropItem(potion);
            taken++;
        }

        return taken;
    }

    /// <summary>
    /// Puts the pieces the owner would bank into the chest, as far as the chest holds them. The
    /// number of pieces put away.
    /// </summary>
    public static int Stash(SosariaCharacter owner, Container chest)
    {
        if (chest is not { Deleted: false })
        {
            return 0;
        }

        var stored = 0;

        foreach (var piece in BankDepositSkill.BankablePieces(owner))
        {
            if (piece is not Gold && BankDepositSkill.BoxTakes(chest, owner, piece) && chest.TryDropItem(owner, piece, false))
            {
                stored++;
            }
        }

        return stored;
    }

    /// <summary>True when the owner carries pieces it would put away.</summary>
    public static bool HasStash(SosariaCharacter owner)
    {
        foreach (var piece in BankDepositSkill.BankablePieces(owner))
        {
            if (piece is not Gold)
            {
                return true;
            }
        }

        return false;
    }
}
