using Server;
using Server.Items;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// A bandage the way a client puts one on: use the bandage, target the patient. On an animal
/// the engine picks Veterinary and Animal Lore, on a person Healing and Anatomy, and runs the
/// heal timer.
/// </summary>
public static class VetBandage
{
    /// <summary>Uses <paramref name="bandage"/> and answers its cursor on <paramref name="patient"/>.</summary>
    public static void Put(Mobile healer, Bandage bandage, Mobile patient)
    {
        bandage.OnDoubleClick(healer);
        healer.Target?.Invoke(healer, patient);
    }

    /// <summary>True when the bandage timer now runs on <paramref name="patient"/>.</summary>
    public static bool TryApply(SosariaCharacter vet, Mobile patient)
    {
        if (vet == null || patient == null || vet.Target != null || BandageContext.GetContext(vet) != null ||
            !vet.InRange(patient, Bandage.Range))
        {
            return false;
        }

        var bandage = vet.Backpack?.FindItemByType<Bandage>();

        if (bandage == null)
        {
            return false;
        }

        Put(vet, bandage, patient);
        return IsTending(vet, patient);
    }

    public static bool IsTending(SosariaCharacter vet, Mobile patient) =>
        BandageContext.GetContext(vet)?.Patient == patient;
}
