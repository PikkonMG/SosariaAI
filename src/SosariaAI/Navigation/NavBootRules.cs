namespace SosariaAI.Navigation;

/// <summary>
/// When the nav graphs are built. A build walks the world's own items: decoration, doors and
/// teleporters. A fresh save has none of them until First Time Setup places them, and a graph
/// built on that bare ground joined roads through furniture: the Yew cellar stairs under the
/// crates, and a Nujel'm node inside a bookcase. So while the world waits for the setup nothing
/// is built and the saved graphs load as they are. The setup's last step checks the saved
/// graphs against the items it placed, as any boot does: a facet with no saved graph or
/// catalog is built, and with <c>nav.rebuildOnBoot</c> every facet is built. A full build on a
/// set-up world takes minutes and gives back the graph that was saved, because the setup
/// places the same items each time. Pure.
/// </summary>
public static class NavBootRules
{
    public const string WaitsForSetupLine =
        "Nav: the world waits for First Time Setup; saved graphs load as they are, and are checked when the setup is done";

    public const string RebuildWaitsLine =
        "Nav rebuild waits for First Time Setup: the world has no decoration, doors or teleporters yet";

    public const string SetupCheckLine =
        "First Time Setup placed the world's items; checking the saved nav graphs against them (a facet with none is built, and every facet when nav.rebuildOnBoot is on)";

    /// <summary>True when the graphs are generated now rather than loaded: asked for, and the world is set up.</summary>
    public static bool Rebuilds(bool forced, bool rebuildOnBoot, bool worldWaits) => !worldWaits && (forced || rebuildOnBoot);
}
