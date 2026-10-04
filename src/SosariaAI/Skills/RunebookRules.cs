using System;
using Server;

namespace SosariaAI.Skills;

/// <summary>How a person travels by one entry of its runebook.</summary>
public enum BookCast
{
    /// <summary>Neither the spell nor a charge will take.</summary>
    None,

    /// <summary>The Recall or Gate spell cast on the entry, from the spellbook or a scroll.</summary>
    Spell,

    /// <summary>One of the book's own charges, as the "use charge" button does.</summary>
    Charge
}

/// <summary>
/// The T2A runebook as players kept it: sixteen marked places in one blessed book in the
/// pack, six charges before Samurai Empire, recharged by dropping recall scrolls on it. A
/// mage casts Recall on an entry; a sword with some Magery uses a charge, which the engine
/// reads like a scroll, so a charge still wants the scroll's Magery. The book stays in the
/// pack: before AOS it takes the one-handed layer, and a hand that held it could not hold a
/// weapon. Loose runes are kept only now and then, and a stack of scrolls rides in the
/// bank box. Pure. No world objects.
/// </summary>
public static class RunebookRules
{
    /// <summary>The engine's runebook holds this many entries.</summary>
    public const int EntryCap = 16;

    /// <summary>Loose recall scrolls kept beside a book, to recharge it on the road.</summary>
    public const int SpareRecallScrolls = 2;

    /// <summary>Blank runes a marking mage keeps loose beside its book, for a new place.</summary>
    public const int SpareBlankRunes = 1;

    /// <summary>A charge is cast like a scroll (<see cref="RecallRules.ScrollMinMagery"/>).</summary>
    public const double ChargeMinMagery = RecallRules.ScrollMinMagery;

    /// <summary>
    /// Below this Magery a person with a charge uses it rather than the spell: a scroll casts
    /// two circles easier, which is why the era's shaky casters read scrolls. A dexxer with
    /// half its skill in Magery fizzled the book spell nearly one cast in two.
    /// </summary>
    public const double ChargePreferredBelowMagery = 55;

    /// <summary>
    /// How a Recall from an entry is made: a charge when the book has one or scrolls can
    /// recharge it and the hand is steady enough for a scroll but not for the book; the
    /// spell when the person can cast it; else a charge when one takes. Gate Travel has no
    /// charge; it is cast or not at all.
    /// </summary>
    public static BookCast HowToRecall(bool canCastSpell, int charges, bool canRecharge, double magery)
    {
        var chargeTakes = (charges > 0 || canRecharge) && magery >= ChargeMinMagery;

        if (chargeTakes && (!canCastSpell || magery < ChargePreferredBelowMagery))
        {
            return BookCast.Charge;
        }

        return canCastSpell ? BookCast.Spell : BookCast.None;
    }

    /// <summary>
    /// Charges a red's empty book gets when it is raised: enough for the recall home to the
    /// Den. The owner's rule: a red raised far from its body recalls home by its book, and a
    /// death takes the scrolls that would charge it.
    /// </summary>
    public const int FreeHomeCharges = 1;

    /// <summary>
    /// True when a raised red's book gets <see cref="FreeHomeCharges"/>: a red with a book
    /// and no charge left, steady enough for a charge to take.
    /// </summary>
    public static bool GetsFreeHomeCharge(bool red, bool hasBook, int charges, double magery) =>
        red && hasBook && charges <= 0 && magery >= ChargeMinMagery;

    /// <summary>A charge recall recharges an empty book first, from the scrolls in the pack.</summary>
    public static bool RechargesFirst(BookCast cast, int charges) => cast == BookCast.Charge && charges <= 0;

    /// <summary>True when the book has room for one more marked rune.</summary>
    public static bool HasRoom(int entries) => entries < EntryCap;

    /// <summary>
    /// Loose recall scrolls that belong in the bank box: whatever the book's charges and the
    /// spare scrolls leave over the person's recall target.
    /// </summary>
    public static int ScrollSurplus(int looseScrolls, int charges, int target) =>
        Math.Clamp(looseScrolls + charges - Math.Max(target, charges + SpareRecallScrolls), 0, Math.Max(0, looseScrolls));

    /// <summary>
    /// A person keeps a book when it has one, or when it travels by magic, is established
    /// and has marked runes to put in it.
    /// </summary>
    public static bool KeepsBook(bool hasBook, bool establishedTraveler, int looseMarked) =>
        hasBook || establishedTraveler && looseMarked > 0;

    /// <summary>
    /// A person who travels by recall and has no book buys one from a scribe: loose runes
    /// are lost to a death and a book holds sixteen marks.
    /// </summary>
    public static bool WantsBook(bool travelsByMagic, bool hasBook) => travelsByMagic && !hasBook;

    /// <summary>The line a recall from a runebook entry writes, easy to count.</summary>
    public static string RecalledFromBookLine(Point3D from, Point3D to) => $"recalled from runebook at {from} to {to}";
}
