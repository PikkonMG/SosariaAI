using System;
using System.Collections.Generic;

namespace SosariaAI.Spawning;

/// <summary>One cloth piece of an outfit: an item type name and its dye.</summary>
public readonly record struct OutfitPiece(string TypeName, int Hue);

/// <summary>
/// The cloth a class wears over its kit: robes and wizard hats for mages, a feathered
/// hat and a cloak for a bard, aprons for smiths and carpenters, a floppy hat on a
/// fisherman. Colours come from the dye tub, hues 2 to 1001, with one main colour and
/// one accent per person; some pieces stay undyed; true black is a rich man's rarity.
/// Armor on a layer always wins over cloth, so a plate helm hides the hat roll. Over body
/// armor nobody wears a kilt, a skirt or a dress: the leggings and the chest show, with a
/// sash, a tunic or a cloak on top.
/// </summary>
public static class OutfitRules
{
    public const int Undyed = 0;
    public const int TrueBlack = 1;
    public const int FirstDyeHue = 2;
    public const int LastDyeHue = 1001;

    public const int UndyedPercent = 30;
    public const int UndyedShoesPercent = 70;
    public const int TrueBlackPercent = 3;

    public const int RobePercent = 85;
    public const int HealerRobePercent = 70;
    public const int WizardHatPercent = 40;
    public const int CloakPercent = 40;
    public const int RangerCloakPercent = 80;
    public const int ThiefCloakPercent = 70;
    public const int HatPercent = 50;
    public const int OvercoatPercent = 30;
    public const int DressPercent = 40;
    public const int ApronPercent = 50;

    public const string Shirt = "Shirt";
    public const string FancyShirt = "FancyShirt";
    public const string LongPants = "LongPants";
    public const string ShortPants = "ShortPants";
    public const string Kilt = "Kilt";
    public const string Skirt = "Skirt";
    public const string Robe = "Robe";
    public const string PlainDress = "PlainDress";
    public const string FancyDress = "FancyDress";
    public const string Cloak = "Cloak";
    public const string Doublet = "Doublet";
    public const string Tunic = "Tunic";
    public const string BodySash = "BodySash";
    public const string FullApron = "FullApron";
    public const string HalfApron = "HalfApron";
    public const string WizardsHat = "WizardsHat";
    public const string Boots = "Boots";
    public const string ThighBoots = "ThighBoots";
    public const string Shoes = "Shoes";
    public const string Sandals = "Sandals";
    public const string Bandana = "Bandana";

    private static readonly string[] Tops = [Shirt, FancyShirt];
    private static readonly string[] Overcoats = [Doublet, Tunic, BodySash];
    private static readonly string[] MaleLegs = [LongPants, LongPants, ShortPants, Kilt];
    private static readonly string[] FemaleLegs = [Skirt, Skirt, LongPants, Kilt];
    private static readonly string[] Dresses = [PlainDress, FancyDress];

    /// <summary>Legs worn under leg armor: the armor owns the layer and hides them.</summary>
    private static readonly string[] ArmoredLegs = [LongPants, ShortPants];

    /// <summary>Cloth that would hide body armor worn beneath it.</summary>
    private static readonly HashSet<string> ArmorHiders = new(StringComparer.OrdinalIgnoreCase)
    {
        Kilt, Skirt, PlainDress, FancyDress
    };
    private static readonly string[] WalkingShoes = [Boots, Shoes, ThighBoots, Sandals];
    private static readonly string[] CasterShoes = [Sandals, Shoes];
    private static readonly string[] FancyHats = ["FeatheredHat", "TricorneHat"];
    private static readonly string[] FieldHats = ["StrawHat", "WideBrimHat", "TallStrawHat"];
    private static readonly string[] SeaHats = ["FloppyHat", "WideBrimHat", "StrawHat"];
    private static readonly string[] RogueHats = ["SkullCap", Bandana];
    private static readonly string[] TravelHats = ["WideBrimHat", "FloppyHat", "Cap"];

    private const int MainHueSalt = 1101;
    private const int AccentHueSalt = 1103;
    private const int UndyedSalt = 1109;
    private const int BlackSalt = 1117;
    private const int PieceSalt = 1123;
    private const int ChanceSalt = 1151;

    /// <summary>The cloth a person wears; <paramref name="armored"/> when body armor lies beneath it.</summary>
    public static List<OutfitPiece> For(PersonProfile profile, string uniqueId, bool armored = false)
    {
        var person = profile ?? PersonProfile.Default;
        var outfit = new Outfit(person, uniqueId, armored);

        switch (person.Class)
        {
            case PersonClass.Mage:
            case PersonClass.Necromancer:
            case PersonClass.TreasureHunter:
            case PersonClass.Alchemist:
            case PersonClass.Scribe:
                outfit.Robed(RobePercent, WizardsHat, WizardHatPercent);
                break;
            case PersonClass.Healer:
                outfit.Robed(HealerRobePercent, Bandana, HatPercent);
                break;
            case PersonClass.Bard:
                outfit.Everyday(FancyShirt);
                outfit.Maybe(Cloak, CloakPercent, accent: true);
                outfit.MaybeFrom(FancyHats, HatPercent);
                break;
            case PersonClass.Thief:
                outfit.Everyday(Shirt);
                outfit.Maybe(Cloak, ThiefCloakPercent, accent: false);
                outfit.MaybeFrom(RogueHats, HatPercent);
                break;
            case PersonClass.Archer:
            case PersonClass.Ranger:
                outfit.Everyday(Shirt);
                outfit.Maybe(Cloak, person.Class == PersonClass.Ranger ? RangerCloakPercent : CloakPercent, accent: true);
                outfit.MaybeFrom(FancyHats, HatPercent);
                break;
            case PersonClass.Tamer:
                outfit.Everyday(Shirt);
                outfit.Maybe(Cloak, CloakPercent, accent: true);
                outfit.MaybeFrom(FieldHats, HatPercent);
                break;
            case PersonClass.Merchant:
                outfit.Everyday(FancyShirt);
                outfit.MaybeDress();
                outfit.MaybeFrom(Overcoats, OvercoatPercent);
                outfit.MaybeFrom(FancyHats, HatPercent);
                break;
            case PersonClass.Smith:
                outfit.Everyday(Shirt);
                outfit.Add(FullApron, outfit.Accent);
                outfit.MaybeFrom(RogueHats, HatPercent);
                break;
            case PersonClass.Carpenter:
            case PersonClass.Tinker:
                outfit.Everyday(Shirt);
                outfit.Add(HalfApron, outfit.Accent);
                break;
            case PersonClass.Tailor:
                outfit.Everyday(FancyShirt);
                outfit.MaybeDress();
                outfit.Maybe(HalfApron, ApronPercent, accent: true);
                break;
            case PersonClass.Miner:
                outfit.Everyday(Shirt);
                outfit.MaybeFrom(RogueHats, HatPercent);
                break;
            case PersonClass.Fisherman:
                outfit.Everyday(Shirt);
                outfit.MaybeFrom(SeaHats, HatPercent);
                break;
            case PersonClass.Lumberjack:
            case PersonClass.Bowyer:
                outfit.Everyday(Shirt);
                outfit.MaybeFrom(TravelHats, HatPercent);
                break;
            default:
                // Fighters: the armor shows; a tunic or sash over it, and a cloak on some.
                outfit.Everyday(Shirt);
                outfit.MaybeFrom(Overcoats, OvercoatPercent);
                outfit.Maybe(Cloak, CloakPercent, accent: true);
                break;
        }

        return outfit.Pieces;
    }

    /// <summary>True when this person's look is a robe: most mages, necromancers and healers.</summary>
    public static bool WearsRobe(PersonProfile profile, string uniqueId)
    {
        var outfit = For(profile, uniqueId);

        for (var i = 0; i < outfit.Count; i++)
        {
            if (outfit[i].TypeName == Robe)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True for cloth that hides body armor worn beneath it: a kilt, a skirt or a dress.</summary>
    public static bool HidesArmor(string typeName) =>
        !string.IsNullOrWhiteSpace(typeName) && ArmorHiders.Contains(typeName);

    /// <summary>A dye-tub hue, undyed cloth, or rarely true black for the rich.</summary>
    public static int DyeHue(string uniqueId, int salt, PersonWealth wealth, int undyedPercent)
    {
        if (wealth == PersonWealth.Rich && PersonDice.Chance(uniqueId, salt + BlackSalt, TrueBlackPercent))
        {
            return TrueBlack;
        }

        if (PersonDice.Chance(uniqueId, salt + UndyedSalt, undyedPercent))
        {
            return Undyed;
        }

        return FirstDyeHue + PersonDice.Roll(uniqueId, salt, LastDyeHue - FirstDyeHue + 1);
    }

    /// <summary>Builds one outfit with the person's two colours and its own dice.</summary>
    private sealed class Outfit
    {
        private readonly PersonProfile _person;
        private readonly string _uniqueId;
        private readonly bool _armored;
        private int _rolls;

        public Outfit(PersonProfile person, string uniqueId, bool armored)
        {
            _person = person;
            _uniqueId = uniqueId;
            _armored = armored;
            Main = DyeHue(uniqueId, MainHueSalt, person.Wealth, UndyedPercent);
            Accent = DyeHue(uniqueId, AccentHueSalt, person.Wealth, UndyedPercent);
        }

        public List<OutfitPiece> Pieces { get; } = [];

        public int Main { get; }

        public int Accent { get; }

        public void Add(string typeName, int hue) => Pieces.Add(new OutfitPiece(typeName, hue));

        /// <summary>A top, legs and shoes: the base every outfit starts from.</summary>
        public void Everyday(string top)
        {
            Add(top == Shirt ? Pick(Tops) : top, Main);
            Add(Pick(_armored ? ArmoredLegs : _person.Female ? FemaleLegs : MaleLegs), Accent);
            Shoes(WalkingShoes);
        }

        /// <summary>A robe on most, everyday clothes on the rest, and a chance of a hat.</summary>
        public void Robed(int robePercent, string hat, int hatPercent)
        {
            if (Chance(robePercent))
            {
                Add(Robe, Main);
                Shoes(CasterShoes);
            }
            else
            {
                Everyday(Shirt);
            }

            Maybe(hat, hatPercent, accent: true);
        }

        public void MaybeDress()
        {
            if (_person.Female && !_armored && Chance(DressPercent))
            {
                Add(Pick(Dresses), Main);
            }
        }

        public void Maybe(string typeName, int percent, bool accent)
        {
            if (Chance(percent))
            {
                Add(typeName, accent ? Accent : Main);
            }
        }

        public void MaybeFrom(string[] typeNames, int percent)
        {
            if (Chance(percent))
            {
                Add(Pick(typeNames), Accent);
            }
        }

        private void Shoes(string[] kinds) =>
            Add(Pick(kinds), DyeHue(_uniqueId, PieceSalt + _rolls++, _person.Wealth, UndyedShoesPercent));

        private bool Chance(int percent) => PersonDice.Chance(_uniqueId, ChanceSalt + _rolls++, percent);

        private string Pick(string[] values) => PersonDice.Pick(_uniqueId, PieceSalt + _rolls++, values);
    }
}
