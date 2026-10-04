using SosariaAI.Combat;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>How a living person can raise a ghost.</summary>
public enum AidMethod
{
    None,
    Spell,
    Bandage
}

/// <summary>
/// Who can raise the dead, and who will. The numbers are the engine's own: Resurrection is
/// eighth circle (Magery 80, 50 mana, bloodmoss, garlic and ginseng); a pre-AOS bandage
/// raise needs Healing 80 and Anatomy 80 and rolls (healing - 68) / 50, less a little for
/// each slip of the fingers.
/// </summary>
public static class ResurrectAid
{
    public const double MageryMin = 80;
    public const double HealingMin = 80;
    public const double AnatomyMin = 80;
    public const int ResurrectMana = 50;
    public const int CastRange = 1;
    public const int GhostSeekRange = 20;

    /// <summary>
    /// A party member raises a fallen mate it can walk to on its own feet: the raise's walk is
    /// one leg of the engine's path. Bran walked for Sela's ghost 58 times from Britain while
    /// she stood dead at the Den, twelve hundred tiles off, and every walk failed at once.
    /// </summary>
    public const int PartyAidRange = NavLimits.SoftLegDistance;

    /// <summary>
    /// A red ghost calls a gang mate this far off to come and raise it, past the sight scan
    /// (<see cref="GhostSeekRange"/>): the mate walks one leg of the engine's path, the same
    /// reach a party mate answers from (<see cref="PartyAidRange"/>). Farther than that the walk
    /// fails at once, as Bran's 58 walks for Sela did.
    /// </summary>
    public const int GangAidRange = PartyAidRange;

    public const double BandageChanceFloor = 68.0;
    public const double BandageChanceSpan = 50.0;
    public const double SlipPenalty = 0.02;

    /// <summary>A person this close who fights the helper stops the raise: nobody kneels under the blows.</summary>
    public const int FoeRange = 12;

    public const string HelperDownWhy = "the helper is down";
    public const string GhostGoneWhy = "the ghost is gone";
    public const string OtherMapWhy = "the ghost left the facet";
    public const string TimeUpWhy = "the ghost was not raised in time";
    public const string NoWalkWhy = "no walk to the ghost";
    public const string NoMeansWhy = "no reagents, mana or bandages left";
    public const string WordsRefusedWhy = "the words would not begin";
    public const string KillerNearWhy = "the killer stands near the ghost";
    public const string WantedUnderGuardsWhy = "the guards want the ghost where it stands";

    /// <summary>A fallen party mate this far off is one the helper walks to and raises.</summary>
    public static bool InPartyReach(bool sameMap, int distance) => sameMap && distance >= 0 && distance <= PartyAidRange;

    /// <summary>
    /// How far off a red ghost calls a living red to come and raise it: a gang mate from
    /// <see cref="GangAidRange"/>, in sight or not, as a mate knows where its mate fell; any other
    /// red only within the sight scan (<see cref="GhostSeekRange"/>), and only one it sees.
    /// </summary>
    public static int RedCallRange(bool sameGang) => sameGang ? GangAidRange : GhostSeekRange;

    /// <summary>A red ghost calls a gang mate before any other red, and of two alike the nearer.</summary>
    public static bool CallsBefore(bool sameGang, int distance, bool bestSameGang, int bestDistance) =>
        sameGang != bestSameGang ? sameGang : distance < bestDistance;

    /// <summary>The skills alone allow a raise of some kind. Cheap enough to ask of everyone on a scan.</summary>
    public static bool SkilledToRaise(double magery, double healing, double anatomy) =>
        magery >= MageryMin || BandageSkilled(healing, anatomy);

    /// <summary>The engine checks both skills before it rolls a bandage raise at all.</summary>
    public static bool BandageSkilled(double healing, double anatomy) =>
        healing >= HealingMin && anatomy >= AnatomyMin;

    /// <summary>A mage says the words; a healer kneels with a bandage only when it cannot cast.</summary>
    public static AidMethod MethodFor(
        double magery,
        int mana,
        bool knowsSpell,
        bool hasReagents,
        double healing,
        double anatomy,
        bool hasBandage
    )
    {
        if (magery >= MageryMin && mana >= ResurrectMana && knowsSpell && hasReagents)
        {
            return AidMethod.Spell;
        }

        return BandageSkilled(healing, anatomy) && hasBandage ? AidMethod.Bandage : AidMethod.None;
    }

    /// <summary>
    /// True when the guards take this person the moment it stands up: a criminal or a red under
    /// the guards. A raise there helps nobody and greys the helper: a red ghost raised in Yew
    /// died to the guards one second later.
    /// </summary>
    public static bool WantedUnderGuards(bool criminal, bool murderer, bool underGuards) =>
        underGuards && (criminal || murderer);

    /// <summary>
    /// Nobody raises the one they killed or a sworn enemy (the other side of Order and Chaos, a
    /// guild at war): a raise by the foe only sets up the next kill. Nobody raises a criminal or
    /// a red under the guards (<see cref="WantedUnderGuards"/>). Help does not cross the red
    /// line unless the two are bonded by party, guild or friendship. A red raises any red ghost
    /// it sees, asked or not: reds stand by reds against the blues, and the owner wants reds to
    /// help reds up. A good-natured person raises any blue it sees; a neutral one raises a
    /// stranger who asks; an outlaw raises no blue stranger.
    /// </summary>
    public static bool Willing(
        bool helperIsRed,
        bool fallenIsRed,
        bool helperIsFoe,
        bool fallenWantedUnderGuards,
        bool bonded,
        DispositionKind disposition,
        bool asked
    )
    {
        if (helperIsFoe || fallenWantedUnderGuards)
        {
            return false;
        }

        if (helperIsRed != fallenIsRed && !bonded)
        {
            return false;
        }

        if (bonded || helperIsRed && fallenIsRed)
        {
            return true;
        }

        return disposition switch
        {
            DispositionKind.Lawful => true,
            DispositionKind.Neutral => asked,
            _ => false
        };
    }

    /// <summary>The engine's pre-AOS bandage raise chance.</summary>
    public static double BandageChance(double healing, int slips) =>
        (healing - BandageChanceFloor) / BandageChanceSpan - slips * SlipPenalty;

    public static bool BandageRaises(double healing, double anatomy, int slips, double roll) =>
        BandageSkilled(healing, anatomy) && BandageChance(healing, slips) > roll;

    /// <summary>Every try was a fizzle, a disturbed cast or a failed bandage roll.</summary>
    public static string TriesSpentWhy(int tries) => $"the raise did not take in {tries} tries";

    public static string AttackedWhy(string foe) => $"{foe} attacked the helper";

    /// <summary>The end-line reason names the ghost, so a failed raise says who stayed dead.</summary>
    public static string FailLine(string why, string ghost) =>
        string.IsNullOrWhiteSpace(ghost) ? why : $"{why} ({ghost})";

    /// <summary>
    /// Why a raise must stop now, or null: a foe fights the helper, the one who killed the
    /// ghost still stands over it, or the ghost is a criminal or a red under the guards
    /// (<see cref="WantedUnderGuards"/>). A raise under any of them only sets up the next
    /// death. A red that had just made a kill said "got u, one sec" to a mate beside its own
    /// victim. Asked on every step of a raise, up to the cursor on the ghost: a ghost that
    /// walks in under the guards, or flags gray, while the helper walks over is not raised.
    /// </summary>
    public static string Hindrance(string attackerName, bool killerNearGhost, bool fallenWantedUnderGuards) =>
        !string.IsNullOrEmpty(attackerName) ? AttackedWhy(attackerName)
        : killerNearGhost ? KillerNearWhy
        : fallenWantedUnderGuards ? WantedUnderGuardsWhy
        : null;

    /// <summary>
    /// The hands are busy with something else: words of power, a cursor, a bandage on someone,
    /// or (for a spell) the recovery after a cast. A healer fresh from a fight is still wrapping
    /// its own wounds; it finishes, then kneels. The raise used to fail on the spot.
    /// </summary>
    public static bool HandsBusy(bool casting, bool cursorUp, bool bandaging, bool castRecovering, AidMethod method) =>
        casting || cursorUp || bandaging || method == AidMethod.Spell && castRecovering;
}
