using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// The world side of <see cref="SpareKitRules"/>: the spare kit bag in the bank box of a red or
/// a blue fighter, packed once on a bind, emptied into the pack at the bank after a death, and
/// filled again from the spare pieces the person carries in: bought ones, and for a red the
/// pieces of its build's roles (<see cref="SpareRole"/>) it kept off the bodies it stripped.
/// World thread only.
/// </summary>
public static class SpareKit
{
    private static readonly ILogger logger = SosariaLog.For(typeof(SpareKit));

    /// <summary>The spare kit bag in the person's bank box, or null.</summary>
    public static Container BagOf(SosariaCharacter character)
    {
        foreach (var item in character?.BankBox?.Items ?? [])
        {
            if (item is Container { Deleted: false } bag && bag.Name == SpareKitRules.BagName)
            {
                return bag;
            }
        }

        return null;
    }

    /// <summary>
    /// Packs the spare kit into the bank box of a red or a blue fighter
    /// (<see cref="SpareKitRules.KeepsSpare"/>) when it has none: one of each combat piece of its
    /// build, the reagent piles at <see cref="SpareKitRules.ReagentReserve"/>, and bandages for a
    /// person that heals. The bag in the box keeps it to once; a person already in the world gets
    /// it on its next bind. Logged.
    /// </summary>
    public static void Seed(SosariaCharacter character)
    {
        var keepsSpare = character != null &&
                         SpareKitRules.KeepsSpare(PkRules.IsRed(character.Kills), character.Build?.IsFighter == true);

        if (!SpareKitRules.Seeds(keepsSpare, BagOf(character) != null) ||
            character.BankBox is not { } box)
        {
            return;
        }

        var bag = new Bag { Name = SpareKitRules.BagName };
        box.DropItem(bag);

        var kit = character.Build?.Kit ?? [];

        for (var i = 0; i < kit.Count; i++)
        {
            if (!KitOnceRules.IsCombatKitPiece(kit[i]) ||
                KitResolver.Create(kit[i], name => AssemblyHandler.FindTypeByName(name)) is not { } piece)
            {
                continue;
            }

            if (piece is BaseReagent reagent)
            {
                reagent.Amount = SpareKitRules.ReagentReserve;
            }

            bag.DropItem(piece);
        }

        if (SupplyCheck.ProfileOf(character).Heals)
        {
            bag.DropItem(new Bandage(SpareKitRules.BandageReserve));
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} keeps a spare kit of {Count} pieces in its bank box", character.Name, bag.Items.Count);
        }
    }

    /// <summary>
    /// The pieces the bag holds that the person lacks and takes out at the bank
    /// (<see cref="SpareKitRules.TakesOut"/>): the kit piece itself, or a piece of its role. A
    /// role the person already wears or carries counts as carried, so a plate helm on the head
    /// leaves the kit's helmet in the bag.
    /// </summary>
    public static List<Item> PiecesToTake(SosariaCharacter character)
    {
        var pieces = new List<Item>();

        if (BagOf(character) is not { } bag)
        {
            return pieces;
        }

        var build = KitBuild.Of(character);
        var kit = character.Build?.Kit ?? [];

        for (var i = 0; i < kit.Count; i++)
        {
            var role = build.RoleOf(kit[i]);
            var piece = FindIn(bag, kit[i]) ?? FindRole(bag, build, role);

            if (SpareKitRules.TakesOut(KitOnceRules.IsCombatKitPiece(kit[i]), Carries(character, build, kit[i], role), piece != null, piece?.Stackable == true) &&
                !pieces.Contains(piece))
            {
                pieces.Add(piece);
            }
        }

        return pieces;
    }

    /// <summary>
    /// At the counter, box open: takes the pieces the person lacks out of the bag, dresses for
    /// its build from the pack, draws its weapon, and puts the spares it carries into the bag.
    /// A person with no bag does nothing here. Logged.
    /// </summary>
    public static void AtCounter(SosariaCharacter character)
    {
        if (BagOf(character) is not { } bag || character.Backpack is not { } pack)
        {
            return;
        }

        ReArm(character);
        GearEquip.DressForBuild(character);
        GearEquip.EquipReadyWeapon(character);
        Stow(character, bag, pack);
    }

    /// <summary>
    /// The combat pieces of the kit that neither the bag nor the pack holds, as the piece or
    /// its role, in kit order; none with no bag.
    /// </summary>
    public static List<string> Lacking(SosariaCharacter character)
    {
        var lacking = new List<string>();

        if (BagOf(character) is not { } bag)
        {
            return lacking;
        }

        var build = KitBuild.Of(character);
        var kit = character.Build?.Kit ?? [];

        for (var i = 0; i < kit.Count; i++)
        {
            var role = build.RoleOf(kit[i]);

            if (SpareKitRules.Lacks(
                    KitOnceRules.IsCombatKitPiece(kit[i]),
                    FindIn(bag, kit[i]) != null || FindRole(bag, build, role) != null,
                    FindIn(character.Backpack, kit[i]) != null || FindRole(character.Backpack, build, role) != null
                ))
            {
                lacking.Add(kit[i]);
            }
        }

        return lacking;
    }

    /// <summary>
    /// The loose pack pieces a red keeps from the pawn counter for its bag or its back
    /// (<see cref="SpareKitRules.Keeps"/>): the best one of each role of its build that the bag
    /// or its body lacks. Empty for anyone without a bag.
    /// </summary>
    public static HashSet<Item> KeptLoot(SosariaCharacter character)
    {
        var kept = new HashSet<Item>();

        if (character == null || !PkRules.IsRed(character.Kills) || BagOf(character) is not { } bag ||
            character.Backpack is not { } pack)
        {
            return kept;
        }

        var build = KitBuild.Of(character);
        var best = new Dictionary<SpareRole, Item>();

        foreach (var item in pack.Items)
        {
            if (item is not (BaseArmor or BaseWeapon) || item.Deleted)
            {
                continue;
            }

            var name = item.GetType().Name;
            var role = build.RoleOf(name);

            if (SpareKitRules.Keeps(build.Fits(role, name), FindRole(bag, build, role) != null, Wears(character, build, role), carriedLoose: false) &&
                (!best.TryGetValue(role, out var rival) || GearScore.RankOf(item) > GearScore.RankOf(rival)))
            {
                best[role] = item;
            }
        }

        kept.UnionWith(best.Values);
        return kept;
    }

    /// <summary>
    /// A red stripping a body keeps the armor, shield and weapon of its build's roles that its
    /// bag or its back lacks (<see cref="SpareKitRules.Keeps"/>), within what it can carry
    /// (<see cref="LootRules.HasRoom"/>). The pieces ride to the Den bank and into the bag.
    /// Logged.
    /// </summary>
    public static void KeepFromBody(SosariaCharacter red, Corpse corpse)
    {
        if (corpse == null || BagOf(red) is not { } bag || red.Backpack is not { } pack)
        {
            return;
        }

        var build = KitBuild.Of(red);
        var names = new List<string>();

        foreach (var item in new List<Item>(corpse.Items))
        {
            if (item is not (BaseArmor or BaseWeapon) || item.Deleted || !item.Movable ||
                item.LootType is LootType.Blessed or LootType.Newbied)
            {
                continue;
            }

            var name = item.GetType().Name;
            var role = build.RoleOf(name);

            if (!SpareKitRules.Keeps(build.Fits(role, name), FindRole(bag, build, role) != null, Wears(red, build, role), FindRole(pack, build, role) != null) ||
                !LootRules.HasRoom(red.TotalWeight, red.MaxWeight, item.PileWeight + item.TotalWeight) ||
                !CorpseLoot.TryLift(red, item))
            {
                continue;
            }

            names.Add(name);
        }

        if (names.Count > 0 && SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} kept {Pieces} off the body of {Victim} for its spare kit",
                red.Name,
                string.Join(", ", names),
                corpse.Owner?.Name
            );
        }
    }

    /// <summary>
    /// True when a red must go back to the Den's bank before it rides out
    /// (<see cref="SpareKitRules.MustReArm"/>): it lacks arms, and the bag or a shop can arm it.
    /// The bag and the shops are looked at only for a red that lacks arms.
    /// </summary>
    public static bool MustReArm(SosariaCharacter character)
    {
        if (character == null || !PkRules.IsRed(character.Kills))
        {
            return false;
        }

        var lacksArms = SpareKitRules.LacksArms(
            character.IsCombatKitMissing(),
            ClassBuilds.TemplateOf(character)?.Weapon != null,
            GearEquip.CanArm(character)
        );

        return SpareKitRules.MustReArm(
            red: true,
            lacksArms,
            lacksArms && (PiecesToTake(character).Count > 0 || UpgradeGearSkill.OfferFor(character) != null)
        );
    }

    /// <summary>
    /// True when the person has its combat kit, and a weapon to hold when its build carries one:
    /// armed, in a player's word (the other side of <see cref="MustReArm"/>).
    /// </summary>
    public static bool Armed(SosariaCharacter character) =>
        character != null && !character.IsCombatKitMissing() &&
        (ClassBuilds.TemplateOf(character)?.Weapon == null || GearEquip.CanArm(character));

    // Takes the pieces the person lacks out of the bag. Logged.
    private static void ReArm(SosariaCharacter character)
    {
        var pieces = PiecesToTake(character);

        if (pieces.Count == 0)
        {
            return;
        }

        var names = new List<string>(pieces.Count);

        for (var i = 0; i < pieces.Count; i++)
        {
            character.AddToBackpack(pieces[i]);
            names.Add(pieces[i].GetType().Name);
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} re-armed from the spare kit in its bank box: {Pieces}",
                character.Name,
                string.Join(", ", names)
            );
        }
    }

    /// <summary>
    /// Puts into the bag each loose piece of armor or weapon that fits the kit, by its role or
    /// as a combat piece of the kit itself, when the bag holds none of it and the person's own
    /// place for it is already dressed (<see cref="SpareKitRules.Stows"/>): a bought spare, a
    /// piece kept off a body, or a second piece picked up on the road. Logged.
    /// </summary>
    private static void Stow(SosariaCharacter character, Container bag, Container pack)
    {
        var build = KitBuild.Of(character);
        var names = new List<string>();

        foreach (var loose in new List<Item>(pack.Items))
        {
            if (loose is not (BaseArmor or BaseWeapon) || loose.Deleted)
            {
                continue;
            }

            var name = loose.GetType().Name;
            var role = build.RoleOf(name);
            var kitPiece = KitOnceRules.IsCombatKitPiece(name) && WorkerTools.IsKitItem(character, loose);
            var inBag = FindIn(bag, name) != null || FindRole(bag, build, role) != null;
            var looseSpare = loose is BaseWeapon ? GearScore.HasWeapon(character) : character.FindItemOnLayer(loose.Layer) != null;

            if (!SpareKitRules.Stows(build.Fits(role, name) || kitPiece, inBag, looseSpare))
            {
                continue;
            }

            bag.DropItem(loose);
            names.Add(name);
        }

        if (names.Count > 0 && SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} put {Count} spare pieces into the spare kit in its bank box: {Pieces}",
                character.Name,
                names.Count,
                string.Join(", ", names)
            );
        }
    }

    /// <summary>
    /// True when the person carries the kit piece or its role: the piece itself, any weapon to
    /// draw for the weapon, or a worn or carried piece of the role for a shield or armor.
    /// </summary>
    private static bool Carries(SosariaCharacter character, KitBuild build, string kitPiece, SpareRole role) =>
        character.HasKitItem(kitPiece) ||
        role.Kind switch
        {
            SpareRoleKind.Weapon => GearEquip.CanArm(character),
            SpareRoleKind.None => false,
            _ => Wears(character, build, role) || FindRole(character.Backpack, build, role) != null
        };

    /// <summary>
    /// True when the person wears a piece of the role: a weapon in hand, a shield on the arm, or
    /// armor on the slot's layer or of the slot by its type (a skirt covers the legs).
    /// </summary>
    private static bool Wears(SosariaCharacter character, KitBuild build, SpareRole role)
    {
        switch (role.Kind)
        {
            case SpareRoleKind.Weapon:
                return GearScore.HasWeapon(character);
            case SpareRoleKind.Shield:
                return character.FindItemOnLayer(Layer.TwoHanded) is BaseShield;
            case SpareRoleKind.Armor:
                if (character.FindItemOnLayer(GearScore.LayerOf(role.Slot)) is BaseArmor)
                {
                    return true;
                }

                foreach (var worn in character.Items)
                {
                    if (worn is BaseArmor && build.RoleOf(worn.GetType().Name) == role)
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    /// <summary>The first top-level piece of the role that fits the build in the container, or null.</summary>
    private static Item FindRole(Container container, KitBuild build, SpareRole role)
    {
        if (container == null || role.Kind == SpareRoleKind.None)
        {
            return null;
        }

        foreach (var item in container.Items)
        {
            if (item is not (BaseArmor or BaseWeapon) || item.Deleted)
            {
                continue;
            }

            var name = item.GetType().Name;

            if (build.RoleOf(name) == role && build.Fits(role, name))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>The first item of the kit type <paramref name="typeName"/> in the container, or null.</summary>
    private static Item FindIn(Container container, string typeName)
    {
        var type = container == null ? null : AssemblyHandler.FindTypeByName(typeName);
        return type == null ? null : container.FindItemByType(type);
    }

    /// <summary>The facts of a person's build that say what role a piece fills and whether it fits.</summary>
    private readonly record struct KitBuild(
        string WeaponRow,
        string KitWeapon,
        bool CarriesShield,
        IReadOnlyList<GearSlot> Slots,
        int WearCeiling
    )
    {
        public static KitBuild Of(SosariaCharacter character)
        {
            var template = ClassBuilds.TemplateOf(character);
            var row = template.Weapon;

            return new KitBuild(
                row,
                row == null ? null : UpgradeGearSkill.KitWeapon(character.Build?.Kit, row),
                template.Shield,
                GearLadder.Slots(template.Armor),
                GearLadder.WearCeiling(template)
            );
        }

        public SpareRole RoleOf(string typeName) => SpareKitRules.RoleOf(typeName, WeaponRow, KitWeapon);

        public bool Fits(SpareRole role, string typeName) =>
            SpareKitRules.FitsBuild(role, CarriesShield, Slots, GearPlan.ScoreOf(typeName), WearCeiling);
    }
}
