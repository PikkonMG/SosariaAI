using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;

namespace SosariaAI.Spawning;

/// <summary>
/// Gives each copy its own persona from a base voice plus part pools, rolled from the
/// unique id so a reboot rebuilds the same person. The person's traits lean its drives:
/// a brave copy has more valor, a greedy one more greed. Only parts that fit the era band
/// are rolled.
/// </summary>
public static class PersonaComposer
{
    public const double DriveOffsetRange = 0.15;
    public const int HourOffsetRange = 2;
    public const int BaseLikeTake = 1;
    public const int PartLikeTake = 2;
    public const int BaseLineTake = 3;
    public const int PartLineTake = 3;
    public const double TraitDriveShift = 0.2;
    public const double CautionDriveShift = 0.1;

    private const int DriveOffsetSteps = 15;
    private const int OriginSalt = 101;
    private const int HabitSalt = 103;
    private const int WantSalt = 107;
    private const int VoiceSalt = 109;
    private const int LikeBaseSalt = 113;
    private const int LikePartSalt = 127;
    private const int DislikeBaseSalt = 131;
    private const int DislikePartSalt = 137;
    private const int IdleBaseSalt = 139;
    private const int IdlePartSalt = 149;
    private const int GreetingBaseSalt = 151;
    private const int GreetingPartSalt = 157;
    private const int ReturnBaseSalt = 163;
    private const int ReturnPartSalt = 167;
    private const int CombatBaseSalt = 173;
    private const int CombatPartSalt = 179;
    private const int LootBaseSalt = 181;
    private const int LootPartSalt = 191;
    private const int GreedSalt = 193;
    private const int CautionSalt = 197;
    private const int ValorSalt = 199;
    private const int HourSalt = 211;

    public static Persona Compose(
        string uniqueId,
        Persona basePersona,
        string job,
        PersonaPartCatalog parts,
        EraBand band,
        PersonTrait traits = PersonTrait.None
    )
    {
        var source = basePersona ?? Persona.CreateNeutral();

        if (!WorkSites.IsCopy(uniqueId))
        {
            return source;
        }

        var catalog = parts ?? PersonaPartCatalog.Empty;
        var work = string.IsNullOrWhiteSpace(job)
            ? FirstJob(source)
            : job;
        var takenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bannedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var takenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return new Persona
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            Jobs = CopyList(source.Jobs),
            Disposition = source.Disposition,
            Background = ComposeBackground(uniqueId, source, work, catalog, band, takenIds, bannedIds),
            Voice = ComposeVoice(uniqueId, source, work, catalog, band, takenIds, bannedIds),
            Likes = MixItems(
                source.Likes,
                catalog.For(PersonaPartsFile.PoolLikes, work, band),
                BaseLikeTake,
                PartLikeTake,
                uniqueId,
                LikeBaseSalt,
                LikePartSalt,
                takenIds,
                bannedIds,
                takenTexts
            ),
            Dislikes = MixItems(
                source.Dislikes,
                catalog.For(PersonaPartsFile.PoolDislikes, work, band),
                BaseLikeTake,
                PartLikeTake,
                uniqueId,
                DislikeBaseSalt,
                DislikePartSalt,
                takenIds,
                bannedIds,
                takenTexts
            ),
            IdleLines = MixItems(
                source.IdleLines,
                catalog.For(PersonaPartsFile.PoolIdle, work, band),
                BaseLineTake,
                PartLineTake,
                uniqueId,
                IdleBaseSalt,
                IdlePartSalt,
                takenIds,
                bannedIds,
                null
            ),
            Greetings = MixItems(
                source.Greetings,
                catalog.For(PersonaPartsFile.PoolGreeting, work, band),
                BaseLineTake,
                PartLineTake,
                uniqueId,
                GreetingBaseSalt,
                GreetingPartSalt,
                takenIds,
                bannedIds,
                null
            ),
            ReturnLines = MixItems(
                source.ReturnLines,
                catalog.For(PersonaPartsFile.PoolReturn, work, band),
                BaseLineTake,
                PartLineTake,
                uniqueId,
                ReturnBaseSalt,
                ReturnPartSalt,
                takenIds,
                bannedIds,
                null
            ),
            CombatLines = MixItems(
                source.CombatLines,
                catalog.For(PersonaPartsFile.PoolCombat, work, band),
                BaseLineTake,
                PartLineTake,
                uniqueId,
                CombatBaseSalt,
                CombatPartSalt,
                takenIds,
                bannedIds,
                null
            ),
            LootLines = MixItems(
                source.LootLines,
                catalog.For(PersonaPartsFile.PoolLoot, work, band),
                BaseLineTake,
                PartLineTake,
                uniqueId,
                LootBaseSalt,
                LootPartSalt,
                takenIds,
                bannedIds,
                null
            ),
            Drives = ShiftDrives(uniqueId, source, traits),
            ActiveStartHour = ShiftHour(uniqueId, source.ActiveStartHour, source.ActiveEndHour),
            ActiveEndHour = ShiftHour(uniqueId, source.ActiveEndHour, source.ActiveStartHour)
        };
    }

    private static string ComposeBackground(
        string uniqueId,
        Persona source,
        string job,
        PersonaPartCatalog catalog,
        EraBand band,
        HashSet<string> takenIds,
        HashSet<string> bannedIds
    )
    {
        var origin = PickPart(
            catalog.For(PersonaPartsFile.PoolOrigin, job, band),
            uniqueId,
            OriginSalt,
            takenIds,
            bannedIds
        );

        if (origin != null)
        {
            Remember(origin, takenIds, bannedIds);
        }

        var habit = PickPart(
            catalog.For(PersonaPartsFile.PoolHabit, job, band),
            uniqueId,
            HabitSalt,
            takenIds,
            bannedIds
        );

        if (habit != null)
        {
            Remember(habit, takenIds, bannedIds);
        }

        var want = PickPart(
            catalog.For(PersonaPartsFile.PoolWant, job, band),
            uniqueId,
            WantSalt,
            takenIds,
            bannedIds
        );

        if (origin == null || habit == null || want == null)
        {
            return source.Background;
        }

        Remember(want, takenIds, bannedIds);
        return string.Join(" ", origin.Text.Trim(), habit.Text.Trim(), want.Text.Trim());
    }

    private static string ComposeVoice(
        string uniqueId,
        Persona source,
        string job,
        PersonaPartCatalog catalog,
        EraBand band,
        HashSet<string> takenIds,
        HashSet<string> bannedIds
    )
    {
        var extra = PickPart(
            catalog.For(PersonaPartsFile.PoolVoice, job, band),
            uniqueId,
            VoiceSalt,
            takenIds,
            bannedIds
        );

        if (extra == null)
        {
            return source.Voice;
        }

        Remember(extra, takenIds, bannedIds);

        if (string.IsNullOrWhiteSpace(source.Voice))
        {
            return extra.Text;
        }

        return $"{source.Voice.TrimEnd()} {extra.Text.Trim()}";
    }

    private static List<string> MixItems(
        List<string> baseItems,
        IReadOnlyList<PersonaPart> parts,
        int baseTake,
        int partTake,
        string uniqueId,
        int baseSalt,
        int partSalt,
        HashSet<string> takenIds,
        HashSet<string> bannedIds,
        HashSet<string> takenTexts
    )
    {
        if (parts == null || parts.Count == 0)
        {
            return CopyList(baseItems);
        }

        var mixed = PickTexts(baseItems, baseTake, uniqueId, baseSalt, takenTexts);

        if (takenTexts != null)
        {
            for (var i = 0; i < mixed.Count; i++)
            {
                takenTexts.Add(mixed[i]);
            }
        }

        var start = Mod(WorkSites.StableRoll(uniqueId, partSalt), parts.Count);
        var added = 0;

        for (var n = 0; n < parts.Count && added < partTake; n++)
        {
            var part = parts[(start + n) % parts.Count];

            if (!CanTake(part, takenIds, bannedIds, takenTexts))
            {
                continue;
            }

            mixed.Add(part.Text);
            Remember(part, takenIds, bannedIds);
            takenTexts?.Add(part.Text);
            added++;
        }

        return mixed;
    }

    private static List<string> PickTexts(
        List<string> source,
        int take,
        string uniqueId,
        int salt,
        HashSet<string> takenTexts
    )
    {
        var usable = new List<string>();

        if (source != null)
        {
            for (var i = 0; i < source.Count; i++)
            {
                var text = source[i];

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (takenTexts != null && takenTexts.Contains(text))
                {
                    continue;
                }

                usable.Add(text);
            }
        }

        if (usable.Count <= take)
        {
            return usable;
        }

        var start = Mod(WorkSites.StableRoll(uniqueId, salt), usable.Count);
        var picked = new List<string>(take);

        for (var n = 0; n < take; n++)
        {
            picked.Add(usable[(start + n) % usable.Count]);
        }

        return picked;
    }

    private static PersonaPart PickPart(
        IReadOnlyList<PersonaPart> candidates,
        string uniqueId,
        int salt,
        HashSet<string> takenIds,
        HashSet<string> bannedIds
    )
    {
        if (candidates == null || candidates.Count == 0)
        {
            return null;
        }

        var start = Mod(WorkSites.StableRoll(uniqueId, salt), candidates.Count);

        for (var n = 0; n < candidates.Count; n++)
        {
            var part = candidates[(start + n) % candidates.Count];

            if (CanTake(part, takenIds, bannedIds, takenTexts: null))
            {
                return part;
            }
        }

        return null;
    }

    private static bool CanTake(
        PersonaPart part,
        HashSet<string> takenIds,
        HashSet<string> bannedIds,
        HashSet<string> takenTexts
    )
    {
        if (part == null || string.IsNullOrWhiteSpace(part.Id) || string.IsNullOrWhiteSpace(part.Text))
        {
            return false;
        }

        if (takenIds.Contains(part.Id) || bannedIds.Contains(part.Id))
        {
            return false;
        }

        if (takenTexts != null && takenTexts.Contains(part.Text))
        {
            return false;
        }

        if (part.Exclude == null)
        {
            return true;
        }

        for (var i = 0; i < part.Exclude.Count; i++)
        {
            if (takenIds.Contains(part.Exclude[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static void Remember(PersonaPart part, HashSet<string> takenIds, HashSet<string> bannedIds)
    {
        takenIds.Add(part.Id);

        if (part.Exclude == null)
        {
            return;
        }

        for (var i = 0; i < part.Exclude.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(part.Exclude[i]))
            {
                bannedIds.Add(part.Exclude[i]);
            }
        }
    }

    private static Dictionary<string, double> ShiftDrives(string uniqueId, Persona source, PersonTrait traits)
    {
        var baseline = source.Drives == null || source.Drives.Count == 0
            ? PersonaDrives.Neutral
            : PersonaDrives.From(source.Drives);
        var shifted = new PersonaDrives(
            baseline.Greed + DriveDelta(uniqueId, GreedSalt) +
            TraitShift(traits, PersonTrait.Greedy, PersonTrait.Generous, TraitDriveShift),
            baseline.Caution + DriveDelta(uniqueId, CautionSalt) +
            TraitShift(traits, PersonTrait.Cautious, PersonTrait.Brave, CautionDriveShift),
            baseline.Valor + DriveDelta(uniqueId, ValorSalt) +
            TraitShift(traits, PersonTrait.Brave, PersonTrait.Cautious, TraitDriveShift),
            isCustom: true
        );
        return PersonasFile.DriveTable(shifted.Greed, shifted.Caution, shifted.Valor);
    }

    /// <summary>Raises a drive for the trait that feeds it and lowers it for the one that starves it.</summary>
    private static double TraitShift(PersonTrait traits, PersonTrait raises, PersonTrait lowers, double amount)
    {
        var shift = 0.0;

        if ((traits & raises) != 0)
        {
            shift += amount;
        }

        if ((traits & lowers) != 0)
        {
            shift -= amount;
        }

        return shift;
    }

    private static double DriveDelta(string uniqueId, int salt)
    {
        var span = DriveOffsetSteps * 2 + 1;
        var raw = Mod(WorkSites.StableRoll(uniqueId, salt), span);
        return (raw - DriveOffsetSteps) * DriveOffsetRange / DriveOffsetSteps;
    }

    private static int? ShiftHour(string uniqueId, int? hour, int? other)
    {
        if (hour is null && other is null)
        {
            return null;
        }

        if (hour is null)
        {
            return null;
        }

        var span = HourOffsetRange * 2 + 1;
        var offset = Mod(WorkSites.StableRoll(uniqueId, HourSalt), span) - HourOffsetRange;
        return DayShapeRules.NormalizeHour(hour.Value + offset);
    }

    private static List<string> CopyList(List<string> source)
    {
        if (source == null || source.Count == 0)
        {
            return [];
        }

        return [.. source];
    }

    private static string FirstJob(Persona source)
    {
        if (source?.Jobs == null || source.Jobs.Count == 0)
        {
            return null;
        }

        return source.Jobs[0];
    }

    private static int Mod(int value, int modulus)
    {
        var raw = value % modulus;
        return raw < 0 ? raw + modulus : raw;
    }
}
