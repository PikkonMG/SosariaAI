using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Server;
using Server.Commands;
using Server.Logging;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Navigation;
using SosariaAI.Logging;
using SosariaAI.Population;
using SosariaAI.Social;

namespace SosariaAI.Mobiles;

/// <summary>
/// Staff commands for watching the characters while testing. Reading the console to work
/// out where somebody is and what they are doing is slow, so these answer in game.
/// </summary>
public static class CharacterCommands
{
    public const string ListCommand = "Sosaria";
    public const string GoToCommand = "SosariaGo";
    public const string BringCommand = "SosariaBring";
    public const string WatchCommand = "SosariaWatch";
    public const string SayCommand = "SosariaSay";
    public const string RouteCommand = "SosariaRoute";
    public const string TileCommand = "SosariaTile";
    public const string PanelCommand = "SosariaPanel";
    public const string StatusCommand = "SosariaStatus";
    public const string MemoryCommand = "SosariaMemory";
    private const string StaffListSeparator = ", ";
    private const string NeedRouteExample = "Give a target, for example: {0} 1425 1695 0, or {0} Connor";

    private const string NoCharacters = "No SosariaAI characters are in the world.";
    private const string NeedNameExample = "Give a name, for example: {0} Connor";
    private const string NeedSayExample = "Give a name and a line, for example: {0} Connor the boat is nearly paid";
    private const string MissingCharacter = "No character named {0}. Use {1} to see them all.";
    private const string NotInWorld = "{0} is not in the world right now.";
    private const string BroughtMessage = "{0} was brought to you.";
    private const string SaidMessage = "{0} says: {1}";

    private static readonly ILogger logger = SosariaLog.For(typeof(CharacterCommands));
    private static readonly ILogger console = SosariaLog.Console(typeof(CharacterCommands));

    public static string ListLogPath() => ConfigFile.PathIn(CharacterCommandText.ListFileName);

    public static void Initialize()
    {
        var commands = StaffCommands();

        foreach (var command in commands)
        {
            CommandSystem.Register(command.Name, command.Access, command.Handler);
        }

        ConsoleInputHandler.RegisterCommand(
            [ListCommand.ToLowerInvariant()],
            "Lists SosariaAI characters.",
            _ => Core.LoopContext.Post(ListToConsole)
        );
        ConsoleInputHandler.RegisterCommand(
            [WatchCommand.ToLowerInvariant()],
            "Prints one SosariaAI character's life and persona.",
            arg => Core.LoopContext.Post(() => WatchToConsole(arg))
        );

        // Say so out loud. A command that silently fails to register is very hard to spot.
        console.Information(
            "Staff commands ready: {Commands}",
            string.Join(StaffListSeparator, UsagesOf(commands).Select(usage => CommandSystem.Prefix + usage))
        );
        EventSink.ServerStarted += PartyChatHook.Install;
        EventSink.ServerStarted += GuildRecruits.Install;
    }

    /// <summary>A staff command: its name, who may use it, and its handler.</summary>
    private readonly record struct StaffCommand(string Name, AccessLevel Access, CommandEventHandler Handler);

    /// <summary>Every staff command of the plugin. Registration and the boot line both read this list.</summary>
    private static StaffCommand[] StaffCommands() =>
    [
        new(ListCommand, AccessLevel.Counselor, OnList),
        new(GoToCommand, AccessLevel.GameMaster, OnGoTo),
        new(BringCommand, AccessLevel.GameMaster, OnBring),
        new(WatchCommand, AccessLevel.Counselor, OnWatch),
        new(MemoryCommand, AccessLevel.Counselor, OnMemory),
        new(SayCommand, AccessLevel.GameMaster, OnSay),
        new(RouteCommand, AccessLevel.Counselor, OnRoute),
        new(TileCommand, AccessLevel.Counselor, OnTile),
        new(PanelCommand, SosariaPanelGump.RequiredAccess, SosariaPanelGump.OnCommand),
        new(StatusCommand, AccessLevel.Counselor, FleetStatus.OnCommand),
        new(NavCommands.RebuildCommand, AccessLevel.Administrator, NavCommands.OnRebuild),
        new(LiveProof.CommandName, AccessLevel.Counselor, LiveProof.OnStart)
    ];

    private static List<string> UsagesOf(StaffCommand[] commands)
    {
        var usages = new List<string>(commands.Length);

        foreach (var command in commands)
        {
            usages.Add(command.Handler.Method.GetCustomAttribute<UsageAttribute>()?.Usage ?? command.Name);
        }

        return usages;
    }

    private static void ListToConsole()
    {
        var report = BuildListReport(staffMap: null, staffLocation: null, out var listed);

        if (listed == 0)
        {
            Console.WriteLine(NoCharacters);
            return;
        }

        Console.WriteLine(report);
        Console.WriteLine(CharacterCommandText.WroteFile(ListLogPath(), listed));
    }

    private static void WatchToConsole(string argument)
    {
        var wanted = argument?.Trim();

        if (string.IsNullOrWhiteSpace(wanted))
        {
            Console.WriteLine(string.Format(NeedNameExample, WatchCommand));
            return;
        }

        var character = FindByName(wanted);

        if (character == null)
        {
            Console.WriteLine(string.Format(MissingCharacter, wanted, ListCommand));
            return;
        }

        var lines = WatchReport(character, observer: null);

        for (var i = 0; i < lines.Count; i++)
        {
            Console.WriteLine(lines[i]);
        }
    }

    [Usage(ListCommand)]
    [Description("Lists every SosariaAI character with where it is, what it is doing, how strong it is, and its ambition.")]
    private static void OnList(CommandEventArgs e)
    {
        var staff = e.Mobile;

        if (staff == null)
        {
            return;
        }

        var report = BuildListReport(staff.Map, staff.Location, out var listed);

        if (listed == 0)
        {
            staff.SendMessage(NoCharacters);
            Console.WriteLine(NoCharacters);
            console.Information("{Staff} ran [Sosaria]: no characters", staff.Name);
            return;
        }

        Console.WriteLine(report);
        staff.SendMessage(CharacterCommandText.WroteFile(ListLogPath(), listed));
        console.Information("{Staff} ran [Sosaria]: {Count} characters -> {Path}", staff.Name, listed, ListLogPath());
    }

    [Usage(GoToCommand + " <name>")]
    [Description("Teleports you to a SosariaAI character, switching facet if you need it.")]
    private static void OnGoTo(CommandEventArgs e) => Move(e, bringToStaff: false);

    [Usage(BringCommand + " <name>")]
    [Description("Teleports a SosariaAI character to you.")]
    private static void OnBring(CommandEventArgs e) => Move(e, bringToStaff: true);

    /// <summary>
    /// [SosariaRoute x y z explains the walk from where the staff member stands to that
    /// tile. [SosariaRoute name explains the walk from that character to the staff member.
    /// The report goes to the staff member and to the activity file.
    /// </summary>
    [Usage(RouteCommand + " <x y z | name>")]
    [Description("Explains the walk from you to a tile, or from a SosariaAI character to you.")]
    private static void OnRoute(CommandEventArgs e)
    {
        var staff = e.Mobile;

        if (staff?.Map == null || staff.Map == Map.Internal)
        {
            return;
        }

        var from = staff.Location;
        Point3D to;

        if (e.Length >= 3 &&
            int.TryParse(e.GetString(0), out var x) &&
            int.TryParse(e.GetString(1), out var y) &&
            int.TryParse(e.GetString(2), out var z))
        {
            to = new Point3D(x, y, z);
        }
        else if (e.Length >= 1 && FindByName(e.ArgString, staff.Map.Name) is { Deleted: false } character &&
                 character.Map == staff.Map)
        {
            from = character.Location;
            to = staff.Location;
        }
        else
        {
            staff.SendMessage(string.Format(NeedRouteExample, CommandSystem.Prefix + RouteCommand));
            return;
        }

        var report = RouteExplain.Describe(staff.Map, staff.Map.Name, from, to);

        foreach (var line in report.Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                staff.SendMessage(line.TrimEnd());
            }
        }

        logger.Information("{Staff} ran [SosariaRoute]: {Report}", staff.Name, report);
    }

    /// <summary>[SosariaTile, or [SosariaTile x y z: what the walkers see at the tile under the staff member, or at the given tile.</summary>
    [Usage(TileCommand + " [x y z]")]
    [Description("Shows what the walkers see at the tile under you, or at the given tile.")]
    private static void OnTile(CommandEventArgs e)
    {
        var staff = e.Mobile;

        if (staff?.Map == null || staff.Map == Map.Internal)
        {
            return;
        }

        var at = staff.Location;

        if (e.Length >= 3 &&
            int.TryParse(e.GetString(0), out var x) &&
            int.TryParse(e.GetString(1), out var y) &&
            int.TryParse(e.GetString(2), out var z))
        {
            at = new Point3D(x, y, z);
        }

        var report = TileExplain.Describe(staff.Map, at.X, at.Y, at.Z);

        foreach (var line in report.Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                staff.SendMessage(line.TrimEnd());
            }
        }

        logger.Information("{Staff} ran [SosariaTile]: {Report}", staff.Name, report);
    }

    [Usage(WatchCommand + " <name>")]
    [Description("Prints one SosariaAI character's ambition, activity, day shape, opinion of you, and recent memory.")]
    private static void OnWatch(CommandEventArgs e)
    {
        var staff = e.Mobile;
        var character = RequireNamed(e, WatchCommand);

        if (staff == null || character == null)
        {
            return;
        }

        var lines = WatchReport(character, staff);

        for (var i = 0; i < lines.Count; i++)
        {
            staff.SendMessage(lines[i]);
        }

        var planLines = CharacterCommandText.PlanLines(character.Planning.Diag);
        for (var i = 0; i < planLines.Count; i++)
        {
            staff.SendMessage(planLines[i]);
        }

        staff.SendMessage(
            $"{CharacterCommandText.HomeDistancePrefix}{HomeLeash.DistanceFromHome(character.Location, character.HomeSpot)} tiles"
        );
        console.Information(
            "{Staff} ran [SosariaWatch] {Name}: {Control} goal={Goal} step={Step} last={Last}",
            staff.Name,
            character.Name,
            character.Planning.Diag.Controller,
            character.Planning.Diag.Goal,
            character.Planning.Diag.StepIndex + " " + character.Planning.Diag.StepSkill,
            character.Planning.Diag.LastStepResult
        );

        if ((character.Build?.Role ?? CharacterRole.Worker) != CharacterRole.Fighter ||
            !GearScore.HasWeapon(character))
        {
            staff.SendMessage(CharacterCommandText.NotAFighter);
        }
    }

    [Usage(MemoryCommand + " <name>")]
    [Description("Prints one SosariaAI character's long-term memory: its warmest bonds and its newest adventures.")]
    private static void OnMemory(CommandEventArgs e)
    {
        var staff = e.Mobile;
        var character = RequireNamed(e, MemoryCommand);

        if (staff == null || character == null)
        {
            return;
        }

        var lines = CharacterCommandText.MemoryReport(MemoryStore.Shared, character.Name, Recall.IdOf(character), Core.Now);

        for (var i = 0; i < lines.Count; i++)
        {
            staff.SendMessage(lines[i]);
        }

        logger.Information("{Staff} ran [{Command}] {Name}: {Report}", staff.Name, MemoryCommand, character.Name, string.Join(StaffListSeparator, lines));
    }

    [Usage(SayCommand + " <name> <text>")]
    [Description("Makes a SosariaAI character speak a line so nearby characters can react.")]
    private static void OnSay(CommandEventArgs e)
    {
        var staff = e.Mobile;
        var character = RequireNamed(e, SayCommand, NeedSayExample);

        if (staff == null || character == null)
        {
            return;
        }

        if (character.Map == null || character.Map == Map.Internal)
        {
            staff.SendMessage(string.Format(NotInWorld, character.Name));
            return;
        }

        var line = CharacterCommandText.TextAfterName(e.ArgString);

        if (string.IsNullOrWhiteSpace(line))
        {
            staff.SendMessage(string.Format(NeedSayExample, SayCommand));
            return;
        }

        // Staff words are said as typed: the speech gate is for the character's own claims.
        character.SpeakScripted(line);
        staff.SendMessage(string.Format(SaidMessage, character.Name, line));
    }

    private static void Move(CommandEventArgs e, bool bringToStaff)
    {
        var command = bringToStaff ? BringCommand : GoToCommand;
        var staff = e.Mobile;
        var character = RequireNamed(e, command);

        if (staff == null || character == null)
        {
            return;
        }

        if (character.Map == null || character.Map == Map.Internal)
        {
            staff.SendMessage(string.Format(NotInWorld, character.Name));
            return;
        }

        if (bringToStaff)
        {
            character.MoveToWorld(staff.Location, staff.Map);
            staff.SendMessage(string.Format(BroughtMessage, character.Name));
            return;
        }

        staff.MoveToWorld(character.Location, character.Map);
        staff.SendMessage(
            $"{character.Name} is {character.CurrentActivity} here, power {CharacterPower.For(character)}."
        );
    }

    private static SosariaCharacter RequireNamed(CommandEventArgs e, string command, string needExample = NeedNameExample)
    {
        var staff = e?.Mobile;

        if (staff == null)
        {
            return null;
        }

        if (e.Arguments is not { Length: > 0 })
        {
            staff.SendMessage(string.Format(needExample, command));
            return null;
        }

        var wanted = e.Arguments[0];
        var character = FindByName(wanted, staff.Map?.Name);

        if (character == null)
        {
            staff.SendMessage(string.Format(MissingCharacter, wanted, ListCommand));
        }

        return character;
    }

    private static SosariaCharacter FindByName(string name, string staffFacet = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var found = All();
        SosariaCharacter onFacet = null;
        SosariaCharacter fallback = null;
        var onFacetRank = CharacterLookup.RankNone;
        var onFacetReplica = int.MaxValue;
        var fallbackRank = CharacterLookup.RankNone;
        var fallbackReplica = int.MaxValue;

        for (var i = 0; i < found.Count; i++)
        {
            var character = found[i];
            var rank = CharacterLookup.MatchRank(
                name,
                character.Name,
                character.Persona?.Id,
                character.CharacterId);

            if (rank == CharacterLookup.RankNone)
            {
                continue;
            }

            var replica = CharacterLookup.ReplicaNumber(character.CharacterId);

            if (CharacterLookup.PreferFacet(staffFacet, character.HomeFacet ?? character.Map?.Name) &&
                (onFacet == null || CharacterLookup.Better(rank, replica, onFacetRank, onFacetReplica)))
            {
                onFacet = character;
                onFacetRank = rank;
                onFacetReplica = replica;
            }

            if (fallback == null || CharacterLookup.Better(rank, replica, fallbackRank, fallbackReplica))
            {
                fallback = character;
                fallbackRank = rank;
                fallbackReplica = replica;
            }
        }

        return onFacet ?? fallback;
    }

    private static string BuildListReport(Map staffMap, Point3D? staffLocation, out int listed)
    {
        var found = All();
        var lines = new List<string>(found.Count);

        for (var i = 0; i < found.Count; i++)
        {
            var character = found[i];
            character.EnsureLife();
            var distance = staffMap == null || staffLocation is null
                ? CharacterCommandText.AnotherFacet
                : CharacterCommandText.DistanceLabel(
                    character.Map == staffMap,
                    NavMetric.Chebyshev(staffLocation.Value, character.Location)
                );
            lines.Add(
                CharacterCommandText.ListLine(
                    character.Name,
                    character.CurrentActivity,
                    character.Location.ToString(),
                    character.Map?.Name,
                    CharacterPower.For(character),
                    distance,
                    character.IsGhost,
                    character.CurrentAmbition(),
                    CharacterLookup.PersonaKey(character.CharacterId, character.Persona?.Id)
                )
            );
        }

        listed = lines.Count;

        if (listed == 0)
        {
            return string.Empty;
        }

        var report = CharacterCommandText.ListReport(lines, listed);

        var path = ListLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, report + "\n", Encoding.UTF8);
        return report;
    }

    private static List<string> WatchReport(SosariaCharacter character, Mobile observer)
    {
        character.EnsureLife();
        var persona = character.Persona;
        var dayPart = character.DayPartAt(Core.Now);
        return CharacterCommandText.WatchLines(
            character.Name,
            character.CurrentActivity,
            character.Location.ToString(),
            character.Map?.Name,
            character.CurrentAmbition(),
            DayShapeRules.LocalHour(Core.Now),
            dayPart,
            persona?.ActiveStartHour,
            persona?.ActiveEndHour,
            Recall.BondOf(MemoryStore.Shared, character, observer),
            character.Memory.Working.Thoughts(),
            character.LastScore ?? character.ScoreNow(),
            DispositionRules.NameOf(character.Disposition),
            GameParty.MemberNames(character),
            persona?.Background,
            persona?.Voice,
            persona?.PickIdleLine(character.Routine?.CurrentSkill?.Name)
        );
    }

    private static List<SosariaCharacter> All()
    {
        var found = new List<SosariaCharacter>();

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is SosariaCharacter { Deleted: false } character)
            {
                found.Add(character);
            }
        }

        return found;
    }
}
