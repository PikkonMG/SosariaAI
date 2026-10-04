using System;
using Server;
using Server.SkillHandlers;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// T2A stealth: hidden first, then the engine's Stealth check grants a few quiet steps. A
/// trainer creeps a tight ring around one spot, one slow step at a time. Before Samurai
/// Empire the engine asks for high Hiding first (Stealth.HidingRequirement), and it refuses
/// anyone in heavy armour. Both refusals reveal: a creeper that fails them only blinks out
/// and back in.
/// </summary>
public static class StealthRules
{
    public const string Kind = SkillKinds.Stealth;

    /// <summary>Tiles from the anchor to the ring a stealth trainer creeps around.</summary>
    public const int RingRadius = 2;

    /// <summary>Seconds between quiet steps: a creep, not a walk.</summary>
    public const int StepGapSeconds = 2;

    private static readonly (int X, int Y)[] Ring =
    [
        (RingRadius, 0),
        (RingRadius, RingRadius),
        (0, RingRadius),
        (-RingRadius, RingRadius),
        (-RingRadius, 0),
        (-RingRadius, -RingRadius),
        (0, -RingRadius),
        (RingRadius, -RingRadius)
    ];

    /// <summary>The engine's pre-AOS armour rating at which Stealth refuses and reveals.</summary>
    public const int ArmorLimit = 26;

    public static readonly TimeSpan StepGap = TimeSpan.FromSeconds(StepGapSeconds);

    public static bool MayBegin(SosariaCharacter thief, double hidingRequirement) =>
        People.InWorld(thief) &&
        MayCreep(thief.Skills.Hiding.Base, hidingRequirement, Stealth.GetArmorRating(thief));

    public static bool HidingMeets(double hiding, double requirement) => hiding >= requirement;

    /// <summary>The engine grants quiet steps only with its Hiding and under its armour limit.</summary>
    public static bool MayCreep(double hidingBase, double hidingRequirement, int armorRating) =>
        HidingMeets(hidingBase, hidingRequirement) && armorRating < ArmorLimit;

    /// <summary>The ring point a trainer creeps toward, by its place in the loop.</summary>
    public static Point3D RingPoint(Point3D anchor, int index)
    {
        var (x, y) = Ring[Math.Abs(index) % Ring.Length];
        return new Point3D(anchor.X + x, anchor.Y + y, anchor.Z);
    }
}
