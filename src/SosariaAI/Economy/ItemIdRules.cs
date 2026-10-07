namespace SosariaAI.Economy;

/// <summary>
/// Who names a looted magic piece, the 1999 way: a person with the Item Identification skill
/// works out its own; one without it pays a merchant at the bank who has the skill. The check
/// is the engine's own (Item Identification, 0 to 100). Live, people walked the shard with
/// "an unidentified" weapon on, and nothing in the shard ever named it. Pure.
/// </summary>
public static class ItemIdRules
{
    /// <summary>A person with this much Item Identification works out its own pieces.</summary>
    public const double SelfIdMinSkill = 50;

    /// <summary>A person with this much Item Identification names other people's pieces for a fee.</summary>
    public const double ServiceMinSkill = 70;

    /// <summary>The gold a merchant takes for one piece it named; nothing for a piece it could not.</summary>
    public const int IdFee = 25;

    /// <summary>A merchant serves a customer this near: the reach of the engine's Item Identification target.</summary>
    public const int ServiceRange = 8;

    /// <summary>The engine's Item Identification window (ItemIdentification): from 0 to 100.</summary>
    public const double CheckMin = 0;

    public const double CheckMax = 100;

    /// <summary>True when a person of this skill works out its own pieces.</summary>
    public static bool IdentifiesOwn(double itemIdSkill) => itemIdSkill >= SelfIdMinSkill;

    /// <summary>True when a person of this skill names pieces for others.</summary>
    public static bool OffersService(double itemIdSkill) => itemIdSkill >= ServiceMinSkill;

    /// <summary>
    /// A customer of the service: someone else, without the skill to do it itself, with an
    /// unidentified piece on it and the fee in its purse.
    /// </summary>
    public static bool IsCustomer(bool isServer, double customerSkill, bool hasUnidentified, int purse) =>
        !isServer && !IdentifiesOwn(customerSkill) && hasUnidentified && purse >= IdFee;

    /// <summary>A piece shows as unidentified when it is magic and nobody named it yet (the engine's label).</summary>
    public static bool ShowsUnidentified(bool magic, bool identified) => magic && !identified;
}
