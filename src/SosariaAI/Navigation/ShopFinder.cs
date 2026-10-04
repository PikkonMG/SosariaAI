using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using SosariaAI.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Skills;

namespace SosariaAI.Navigation;

/// <summary>A shop marker and the live vendor standing at it who stocks the wares.</summary>
public readonly record struct StockedShop(BaseVendor Vendor, Point3D Marker);

/// <summary>
/// The nearest shop of a kind for a character, from the generated destination list.
/// Every trade skill walked to one Britain tile; a cook in Skara or a mage in Jhelom
/// had no route there and never worked. The authored Britain tile stays as the
/// fallback for a kind the list does not know.
/// </summary>
public static class ShopFinder
{
    public const string TavernToken = "tavern";
    public const string MageToken = "vendor:Mage";
    public const string TailorToken = "vendor:Tailor";
    public const string TinkerToken = "vendor:Tinker";
    public const string BowyerToken = "vendor:Bowyer";
    public const string MapmakerToken = "vendor:Mapmaker";
    public const string CarpenterToken = "carpenter";
    public const string SmithToken = "blacksmith";

    public const string ArmorerToken = "vendor:Armorer";
    public const string AnimalTrainerToken = "vendor:AnimalTrainer";
    public const string AlchemistToken = "vendor:Alchemist";
    public const string ProvisionerToken = "vendor:Provisioner";
    public const string WeaponsmithToken = "vendor:Weaponsmith";
    public const string TannerToken = "vendor:Tanner";
    public const string WeaverToken = "vendor:Weaver";
    public const string FishermanToken = "vendor:Fisherman";
    public const string HerbalistToken = "vendor:Herbalist";
    public const string ButcherToken = "vendor:Butcher";
    public const string BakerToken = "vendor:Baker";
    public const string JewelerToken = "vendor:Jeweler";
    public const string ScribeToken = "vendor:Scribe";

    /// <summary>
    /// Markers of one kind looked at, nearest first, for one on the walker's own piece of the
    /// roads (<see cref="Traveler.GoalApart"/>) and, for a stocked shop, where a vendor stands.
    /// </summary>
    public const int StockedLooks = 4;

    /// <summary>How long a marker found with nobody behind the counter is passed over.</summary>
    public static readonly TimeSpan EmptyShopMemory = TimeSpan.FromMinutes(10);

    private static readonly EmptyShops Empty = new();

    /// <summary>
    /// The nearest shop of one of the kinds within the leash where a live vendor stands who
    /// stocks one of <paramref name="wares"/>, or null. A marker is only where the shop was
    /// drawn: 25 buyers walked to one with nobody at it and failed at the counter. A marker
    /// found empty is passed over for <see cref="EmptyShopMemory"/>, and so is one no road
    /// from the walker reaches (<see cref="Traveler.GoalApart"/>). A red passes over every shop
    /// under the guards (<see cref="PkRules.MayVisit"/>): its walk there is refused.
    /// </summary>
    public static StockedShop? NearestStocked(
        SosariaCharacter character,
        IReadOnlyList<string> tokens,
        IReadOnlyList<Type> wares
    ) =>
        character == null
            ? null
            : NearestStocked(character.HomeFacet, character.Map, character.Location, PkRules.IsRed(character.Kills), tokens, wares);

    /// <summary>
    /// <see cref="NearestStocked(SosariaCharacter, IReadOnlyList{string}, IReadOnlyList{Type})"/>
    /// for a walker of <paramref name="facet"/> who stands at <paramref name="from"/> on
    /// <paramref name="map"/>, red or not.
    /// </summary>
    public static StockedShop? NearestStocked(
        string facet,
        Map map,
        Point3D from,
        bool red,
        IReadOnlyList<string> tokens,
        IReadOnlyList<Type> wares
    )
    {
        var catalog = NavWorld.DestinationsFor(facet);

        if (map == null || map == Map.Internal || catalog == null || tokens == null)
        {
            return null;
        }

        var graph = NavWorld.GraphFor(facet);
        var reach = HomeLeash.ConfiguredRadius();
        StockedShop? best = null;
        var bestTiles = int.MaxValue;

        for (var t = 0; t < tokens.Count; t++)
        {
            var markers = catalog.NearestFirst(tokens[t], from, StockedLooks);

            for (var i = 0; i < markers.Count; i++)
            {
                var marker = markers[i].Arrival;
                var tiles = NavMetric.Chebyshev(from, marker);

                if (marker == Point3D.Zero || tiles >= bestTiles ||
                    HomeLeash.BeyondLeash(marker, from, reach) ||
                    Empty.IsEmpty(facet, marker, Core.Now) ||
                    !PkRules.MayVisit(red, GuardCall.IsGuardedPlace(marker, map)) ||
                    Traveler.GoalApart(graph, from, marker))
                {
                    continue;
                }

                var vendors = VendorDeal.VendorsNear(map, marker, VendorDeal.CounterRange);

                if (vendors.Count == 0)
                {
                    NoteEmpty(facet, marker);
                    continue;
                }

                var vendor = vendors.Find(candidate => VendorDeal.ShelfLine(candidate, wares) != null);

                if (vendor != null)
                {
                    best = new StockedShop(vendor, marker);
                    bestTiles = tiles;
                }
            }
        }

        return best;
    }

    /// <summary>A shop reached with nobody behind the counter: passed over for <see cref="EmptyShopMemory"/>.</summary>
    public static void NoteEmpty(SosariaCharacter character, Point3D marker) => NoteEmpty(character?.HomeFacet, marker);

    private static void NoteEmpty(string facet, Point3D marker) => Empty.Note(facet, marker, Core.Now + EmptyShopMemory);

    /// <summary>
    /// The nearest shop of the kind within the character's leash, else the fallback if
    /// that is within the leash, else nothing. Vesper has no smith in the list; its
    /// people walked for Minoc, 420 tiles off, and never got there.
    /// </summary>
    public static Point3D Nearest(SosariaCharacter character, string token, Point3D fallback)
    {
        if (character == null)
        {
            return Point3D.Zero;
        }

        var reach = HomeLeash.ConfiguredRadius();
        var graph = NavWorld.GraphFor(character.HomeFacet);
        var dest = NearestOnWalkersRoads(NavWorld.DestinationsFor(character.HomeFacet), graph, token, character.Location);

        if (dest != null && dest.Arrival != Point3D.Zero &&
            !HomeLeash.BeyondLeash(dest.Arrival, character.Location, reach))
        {
            // The node's floor, not the marker's seed z — raised-floor towns like
            // Moonglow seed z = 0 while the walkable approach sits at z = 20.
            return dest.ApproachPoint(graph);
        }

        return fallback != Point3D.Zero && !HomeLeash.BeyondLeash(fallback, character.Location, reach)
            ? fallback
            : Point3D.Zero;
    }

    /// <summary>
    /// The nearest of the first <see cref="StockedLooks"/> places the token names whose approach
    /// a road from <paramref name="from"/> reaches (<see cref="Traveler.GoalApart"/>); a token
    /// no place answers resolves as the catalog resolves it. Jhelom's shoppers took the shops on
    /// the south island, which no road reaches, as the nearest and walked for them 28 times.
    /// </summary>
    private static Destination NearestOnWalkersRoads(DestinationCatalog catalog, NavGraph graph, string token, Point3D from)
    {
        if (catalog == null)
        {
            return null;
        }

        var matches = catalog.NearestFirst(token, from, StockedLooks);

        if (matches.Count == 0)
        {
            return catalog.Resolve(token, from);
        }

        for (var i = 0; i < matches.Count; i++)
        {
            if (!Traveler.GoalApart(graph, from, matches[i].ApproachPoint(graph)))
            {
                return matches[i];
            }
        }

        return null;
    }
}
