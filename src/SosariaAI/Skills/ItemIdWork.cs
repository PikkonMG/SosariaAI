using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// The world side of <see cref="ItemIdRules"/>: one try of Item Identification on a real
/// piece. A person with the skill names its own unidentified piece; a merchant names the piece
/// of a customer near it, and the customer pays the fee only for a piece that was named. The
/// check is the engine's own (ItemIdentification's target: CheckTargetSkill, then Identified).
/// World thread only.
/// </summary>
public static class ItemIdWork
{
    private static readonly ILogger logger = SosariaLog.For(typeof(ItemIdWork));

    /// <summary>
    /// One rep of Item Identification practice on a real piece: the person's own when it can
    /// name its own, else a customer's when it serves. False when there was no piece to try:
    /// the plain practice roll follows.
    /// </summary>
    public static bool Rep(SosariaCharacter character)
    {
        var skill = character.Skills.ItemID.Value;

        if (ItemIdRules.IdentifiesOwn(skill) && FirstUnidentified(character) is { } own)
        {
            IdentifyOwn(character, own);
            return true;
        }

        if (ItemIdRules.OffersService(skill) && !TradeSessions.IsBusy(character) &&
            FindCustomer(character, out var customer, out var piece))
        {
            Serve(character, customer, piece);
            return true;
        }

        return false;
    }

    /// <summary>A magic weapon or armor piece nobody named yet, as the engine labels it "an unidentified".</summary>
    public static bool ShowsUnidentified(Item item) =>
        item switch
        {
            BaseWeapon weapon => ItemIdRules.ShowsUnidentified(
                weapon.DurabilityLevel > WeaponDurabilityLevel.Regular ||
                weapon.AccuracyLevel > WeaponAccuracyLevel.Regular ||
                weapon.DamageLevel > WeaponDamageLevel.Regular ||
                weapon.Slayer != SlayerName.None,
                weapon.Identified
            ),
            BaseArmor armor => ItemIdRules.ShowsUnidentified(
                armor.Durability != ArmorDurabilityLevel.Regular || armor.ProtectionLevel != ArmorProtectionLevel.Regular,
                armor.Identified
            ),
            _ => false
        };

    /// <summary>The first unidentified piece the person wears, else the first in its pack; null when none.</summary>
    public static Item FirstUnidentified(Mobile person)
    {
        foreach (var worn in person.Items)
        {
            if (ShowsUnidentified(worn))
            {
                return worn;
            }
        }

        if (person.Backpack is not { } pack)
        {
            return null;
        }

        foreach (var weapon in pack.FindItemsByType<BaseWeapon>())
        {
            if (ShowsUnidentified(weapon))
            {
                return weapon;
            }
        }

        foreach (var armor in pack.FindItemsByType<BaseArmor>())
        {
            if (ShowsUnidentified(armor))
            {
                return armor;
            }
        }

        return null;
    }

    // The engine's Item Identification check on the piece; a pass is the named piece.
    private static bool Passes(Mobile identifier, Item piece) =>
        identifier.CheckTargetSkill(SkillName.ItemID, piece, ItemIdRules.CheckMin, ItemIdRules.CheckMax);

    private static void IdentifyOwn(SosariaCharacter character, Item piece)
    {
        var named = Passes(character, piece);

        if (named)
        {
            ((IIdentifiable)piece).Identified = true;
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                named ? "{Name} identified its {Piece}" : "{Name} could not make out its {Piece}",
                character.Name,
                Appraisal.NounOf(piece)
            );
        }
    }

    // The first person in reach (ItemIdRules.ServiceRange) who wants the service and can pay for it.
    private static bool FindCustomer(SosariaCharacter merchant, out SosariaCharacter customer, out Item piece)
    {
        foreach (var mobile in merchant.GetMobilesInRange(ItemIdRules.ServiceRange))
        {
            if (mobile is SosariaCharacter { Deleted: false, Alive: true, Combatant: null } person &&
                !TradeSessions.IsBusy(person) && FirstUnidentified(person) is { } unnamed &&
                ItemIdRules.IsCustomer(person == merchant, person.Skills.ItemID.Value, hasUnidentified: true, TradeHandOff.Purse(person)))
            {
                customer = person;
                piece = unnamed;
                return true;
            }
        }

        customer = null;
        piece = null;
        return false;
    }

    // The merchant tries the piece; a named one is paid for, then marked, and the merchant says what it is.
    private static void Serve(SosariaCharacter merchant, SosariaCharacter customer, Item piece)
    {
        var noun = Appraisal.NounOf(piece);

        if (!Passes(merchant, piece))
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Merchant} could not make out {Customer}'s {Piece}", merchant.Name, customer.Name, noun);
            }

            return;
        }

        if (!TradeHandOff.Pay(customer, merchant, ItemIdRules.IdFee))
        {
            return;
        }

        ((IIdentifiable)piece).Identified = true;
        Talk.Say(merchant, TalkCategory.ItemIdentified, new TalkSlots { Name = customer.Name, Item = noun });

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Merchant} identified {Customer}'s {Piece} for {Gold} gold at {Location}",
                merchant.Name,
                customer.Name,
                noun,
                ItemIdRules.IdFee,
                merchant.Location
            );
        }
    }
}
