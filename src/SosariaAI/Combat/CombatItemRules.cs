using Server.Items;

namespace SosariaAI.Combat;

/// <summary>
/// The fight items a player of the period used against a person: an explosion potion on a foe
/// held in place, and a pouch trapped with Magic Trap opened to break a paralyze. Engine facts:
/// a potion's throw cursor reaches ten tiles before ML (BaseExplosionPotion.ThrowTarget), the
/// potion sits where it lands until its fuse burns down (about four seconds before ML) and then
/// hurts every body two tiles round it the thrower may harm, the thrower too; it lands on the
/// foe's tile, so only a held foe is still there. Any damage frees a paralyzed body
/// (Mobile.Damage), and a Magic Trap on a pouch deals its power to the one who opens it
/// (TrappableContainer.ExecuteMagicTrap), so the pouch frees its owner. Pure.
/// </summary>
public static class CombatItemRules
{
    /// <summary>The blast reaches this far round the spot it lands on (BaseExplosionPotion.ExplosionRange).</summary>
    public const int BlastTiles = 2;

    /// <summary>The throw cursor's reach before ML, and from ML on (BaseExplosionPotion.ThrowTarget).</summary>
    public const int ThrowReachTiles = 10;
    public const int MlThrowReachTiles = 12;

    /// <summary>
    /// A potion burns this long before it blows before ML (BaseExplosionPotion.DetonateTimer:
    /// first tick at 0.75 s, then three more one second apart): a second throw at the same held
    /// foe waits it out, so the two do not land on the same tick of a broken hold.
    /// </summary>
    public const int FuseMs = 3750;

    /// <summary>The power the Magic Trap spell lays on a pouch before AOS (MagicTrapSpell): one point frees the opener.</summary>
    public const int MagicTrapPower = 1;

    public static int ThrowReach(bool ml) => ml ? MlThrowReachTiles : ThrowReachTiles;

    /// <summary>
    /// An explosion potion goes at a person held in place, out of the blast and within the
    /// throw's reach. A thrower that closes to melee walks into its own blast, so only one that
    /// keeps its distance throws. The caller checks that no one else the blast would hurt
    /// stands in it.
    /// </summary>
    public static bool ThrowsExplosion(bool foeIsPerson, bool foeHeld, bool keepsDistance, int distance, bool ml) =>
        foeIsPerson && foeHeld && keepsDistance && distance > BlastTiles && distance <= ThrowReach(ml);

    /// <summary>
    /// A pouch whose opening frees a paralyzed owner: one trapped with Magic Trap. Another trap
    /// poisons or burns harder, and a pouch with none does nothing.
    /// </summary>
    public static bool FreesFromParalyze(TrapType trap) => trap == TrapType.MagicTrap;
}
