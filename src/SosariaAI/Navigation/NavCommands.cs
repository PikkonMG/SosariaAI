using Server;
using Server.Commands;
using SosariaAI.Configuration;

namespace SosariaAI.Navigation;

public static class NavCommands
{
    public const string RebuildCommand = "SosariaRebuildNav";

    /// <summary>[SosariaRebuildNav. <see cref="Mobiles.CharacterCommands"/> registers it with the other staff commands.</summary>
    [Usage(RebuildCommand)]
    [Description("Rebuilds SosariaAI navigation graphs from ModernUO Data files.")]
    public static void OnRebuild(CommandEventArgs e)
    {
        e.Mobile?.SendMessage(NavWorld.Rebuild() ? "Sosaria navigation graphs were rebuilt." : NavBootRules.RebuildWaitsLine);
    }
}
