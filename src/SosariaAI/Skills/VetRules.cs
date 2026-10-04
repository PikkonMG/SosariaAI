using SosariaAI.Configuration;

namespace SosariaAI.Skills;

/// <summary>
/// Classic veterinary: a real bandage on a nearby injured or poisoned animal. The engine
/// runs the Veterinary and Animal Lore checks and the heal.
/// </summary>
public static class VetRules
{
    public const string Kind = SkillKinds.Vet;
    public const int ReachTiles = 8;

    public const string NoPatientWhy = "no hurt beast in reach";
    public const string NoBandageWhy = "no bandage";
    public const string AwayWhy = "out of the world";
    public const string PatientGoneWhy = "the beast died or left";
    public const string NoWalkWhy = "no walk to the beast";
    public const string BandageRefusedWhy = "the bandage would not go on";

    public static bool MayVet(int hits, int hitsMax, bool poisoned) =>
        hits < hitsMax || poisoned;
}
