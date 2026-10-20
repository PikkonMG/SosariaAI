using System;
using Server;

namespace SosariaAI.Behaviour;

/// <summary>What a party leader tells a member to do. Follow is the rest state of a member.</summary>
public enum PartyOrderKind
{
    None,
    Follow,
    Hold,
    GoTo,
    Attack,
    Back,
    Regroup
}

/// <summary>What any member may ask the group for.</summary>
public enum PartyHelpKind
{
    None,
    HealMe,
    WaitForMe,
    Stuck,

    /// <summary>Come fight beside me: the free members take my foe, or walk to me.</summary>
    Backup,

    /// <summary>Tell me where you stand.</summary>
    Where
}

/// <summary>
/// One standing order to a party member: its kind, who gave it and when, and the target or
/// place it names. A new order replaces the old one.
/// </summary>
public sealed record PartyOrder(
    PartyOrderKind Kind,
    Serial From,
    DateTime At,
    Serial Target = default,
    Point3D Place = default,
    string PlaceName = null
)
{
    /// <summary>True when another order asks the same thing, so the member need not start over.</summary>
    public bool SameAs(PartyOrderKind kind, Serial target, Point3D place) =>
        Kind == kind && Target == target && Place == place;
}
