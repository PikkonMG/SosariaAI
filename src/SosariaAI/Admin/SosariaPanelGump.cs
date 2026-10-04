using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Network;
using SosariaAI.Mobiles;

namespace SosariaAI.Admin;

/// <summary>
/// [SosariaPanel: the staff panel. Live counts for the whole shard and the ground around
/// the staff member, staff travel to towns and dungeon entrances, a status page write, and
/// First Time Setup on a new world, which asks first. Nothing here deletes a character or a
/// file.
/// </summary>
public sealed class SosariaPanelGump : DynamicGump
{
    public const AccessLevel RequiredAccess = AccessLevel.GameMaster;

    /// <summary>First Time Setup runs the engine's world commands, so it asks an administrator.</summary>
    public const AccessLevel SetupAccess = AccessLevel.Administrator;

    private const int OriginX = 40;
    private const int OriginY = 40;
    private const int Width = 640;
    private const int Margin = 15;
    private const int LineHeight = 20;
    private const int SectionGap = 8;
    private const int Columns = 4;
    private const int ColumnWidth = (Width - Margin * 2) / Columns;
    private const int ButtonLabelOffset = 35;
    private const int LabelTopOffset = 2;
    private const int BackgroundArt = 5054;
    private const int ButtonUpArt = 4005;
    private const int ButtonDownArt = 4007;
    private const int HeadingHue = 0x35;
    private const int TextHue = 0x481;
    private const int ConfirmWidth = 420;
    private const int ConfirmHeight = 200;

    private const string Title = "SosariaAI staff panel";
    private const string ActionsHeading = "Actions";
    private const string TownsHeading = "Towns";
    private const string DungeonsHeading = "Dungeon entrances";
    private const string NoSpots = "none on this facet";
    private const string RefreshLabel = "Refresh";
    private const string WriteStatusLabel = "Write status page";
    private const string SetupLabel = "★ First Time Setup (run this once on a new world)";
    private const string SetupRunningLabel = "First Time Setup is running... watch your messages";
    private const string SetupNeededBanner = "★ THIS WORLD IS NOT SET UP YET ★  No characters spawn until you run it.";
    private const string SetupDoneLine = "World set up: First Time Setup has been run.";
    private const int WarningHue = 0x26;
    private const string SetupConfirm =
        "First Time Setup builds the world: decorations, doors, signs, teleporters, moongates, and the monster and vendor spawners for each enabled facet. Then the characters come in. Run it once on a new world. Continue?";
    private const string SetupNeedsAdmin = "First Time Setup needs an administrator account.";
    private readonly Map _map;
    private readonly List<string> _lines;
    private readonly List<TravelSpot> _towns;
    private readonly List<TravelSpot> _dungeons;
    private readonly bool _setupNeeded;
    private readonly bool _setupRunning;

    public override bool Singleton => true;

    private SosariaPanelGump(Mobile staff) : base(OriginX, OriginY)
    {
        _map = staff.Map;
        var live = FleetStatus.InWorld();
        var samples = new List<CharacterSample>(live.Count);

        for (var i = 0; i < live.Count; i++)
        {
            samples.Add(FleetStatus.Sample(live[i]));
        }

        var counts = FleetCounts.Of(samples);
        _lines = [PanelRules.WhereLine(staff.Location.ToString(), _map.Name, counts.Live)];

        foreach (var (facet, facetLive) in counts.LiveByFacet)
        {
            _lines.Add(PanelRules.FacetLine(facet, facetLive));
        }

        _lines.Add(PanelRules.StateLine(counts));
        _lines.Add(PanelRules.NearLine(PanelActions.LiveNear(staff).Count));
        _lines.Add(PanelRules.WatchdogLine(FleetStatus.StuckCount, FleetWatchdog.RoadMoves, FleetWatchdog.Rescores));
        _setupNeeded = WorldSetup.Waiting;
        _setupRunning = WorldSetup.IsRunning;

        if (!_setupNeeded && !_setupRunning)
        {
            _lines.Add(SetupDoneLine);
        }

        _towns = PanelActions.TownSpots(_map);
        _dungeons = PanelActions.DungeonSpots(_map);
    }

    [Usage(CharacterCommands.PanelCommand)]
    [Description("Opens the SosariaAI staff panel: counts, travel and the status page.")]
    public static void OnCommand(CommandEventArgs e) => DisplayTo(e.Mobile);

    public static void DisplayTo(Mobile staff)
    {
        if (staff?.NetState == null || staff.AccessLevel < RequiredAccess || !People.InWorld(staff))
        {
            return;
        }

        staff.SendGump(new SosariaPanelGump(staff));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, Width, Height(), BackgroundArt);
        builder.AddAlphaRegion(Margin / 2, Margin / 2, Width - Margin, Height() - Margin);

        var y = Margin;
        builder.AddLabel(Margin, y, HeadingHue, Title);
        y += LineHeight;

        // A world that still needs First Time Setup shows it first, in warning colour, on a
        // row of its own. Once the world is set up the button is gone.
        if (_setupNeeded || _setupRunning)
        {
            builder.AddLabel(Margin, y, WarningHue, SetupNeededBanner);
            y += LineHeight;
            AddWideAction(ref builder, y, PanelRules.ButtonFirstTimeSetup, _setupRunning ? SetupRunningLabel : SetupLabel);
            y += LineHeight + SectionGap;
        }

        for (var i = 0; i < _lines.Count; i++)
        {
            builder.AddLabel(Margin, y, TextHue, _lines[i]);
            y += LineHeight;
        }

        y += SectionGap;
        builder.AddLabel(Margin, y, HeadingHue, ActionsHeading);
        y += LineHeight;
        AddAction(ref builder, 0, y, PanelRules.ButtonRefresh, RefreshLabel);
        AddAction(ref builder, 1, y, PanelRules.ButtonWriteStatus, WriteStatusLabel);
        y += LineHeight + SectionGap;

        y = AddSpots(ref builder, y, TownsHeading, _towns, PanelRules.TownSection);
        AddSpots(ref builder, y + SectionGap, DungeonsHeading, _dungeons, PanelRules.DungeonSection);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var staff = sender.Mobile;

        if (staff == null || staff.AccessLevel < RequiredAccess)
        {
            return;
        }

        switch (info.ButtonID)
        {
            case 0:
                return;
            case PanelRules.ButtonRefresh:
                DisplayTo(staff);
                return;
            case PanelRules.ButtonFirstTimeSetup when WorldSetup.Waiting:
                AskSetup(staff);
                return;
            case PanelRules.ButtonWriteStatus:
                staff.SendMessage(string.Format(FleetStatus.WroteStatus, FleetStatus.WriteNow()));
                DisplayTo(staff);
                return;
        }

        if (PanelRules.TryReadSpot(info.ButtonID, out var section, out var index))
        {
            var spots = section == PanelRules.TownSection ? _towns : _dungeons;

            if (index < spots.Count)
            {
                staff.MoveToWorld(spots[index].At, _map);
                staff.SendMessage(PanelRules.MovedTo(spots[index].Label));
            }
        }

        DisplayTo(staff);
    }

    private static void AskSetup(Mobile staff)
    {
        if (staff.AccessLevel < SetupAccess)
        {
            staff.SendMessage(SetupNeedsAdmin);
            DisplayTo(staff);
            return;
        }

        if (WorldSetup.IsRunning)
        {
            DisplayTo(staff);
            return;
        }

        staff.SendGump(
            new WarningGump(
                SetupConfirm,
                ConfirmWidth,
                ConfirmHeight,
                confirmed =>
                {
                    if (confirmed)
                    {
                        WorldSetup.Run(staff);
                    }

                    DisplayTo(staff);
                }
            )
        );
    }

    private static void AddWideAction(ref DynamicGumpBuilder builder, int y, int buttonId, string label)
    {
        builder.AddButton(Margin, y, ButtonUpArt, ButtonDownArt, buttonId);
        builder.AddLabelCropped(
            Margin + ButtonLabelOffset,
            y + LabelTopOffset,
            Width - Margin * 2 - ButtonLabelOffset,
            LineHeight,
            WarningHue,
            label
        );
    }

    private static void AddAction(ref DynamicGumpBuilder builder, int column, int y, int buttonId, string label)
    {
        var x = Margin + column * ColumnWidth;
        builder.AddButton(x, y, ButtonUpArt, ButtonDownArt, buttonId);
        builder.AddLabelCropped(
            x + ButtonLabelOffset,
            y + LabelTopOffset,
            ColumnWidth - ButtonLabelOffset,
            LineHeight,
            TextHue,
            label
        );
    }

    private static int AddSpots(ref DynamicGumpBuilder builder, int y, string heading, List<TravelSpot> spots, int section)
    {
        builder.AddLabel(Margin, y, HeadingHue, heading);
        y += LineHeight;

        if (spots.Count == 0)
        {
            builder.AddLabel(Margin, y, TextHue, NoSpots);
            return y + LineHeight;
        }

        for (var i = 0; i < spots.Count; i++)
        {
            AddAction(ref builder, i % Columns, y + i / Columns * LineHeight, PanelRules.SpotButton(section, i), spots[i].Label);
        }

        return y + Rows(spots.Count) * LineHeight;
    }

    private int Height() =>
        Margin * 2 +
        LineHeight * (1 + _lines.Count) +
        (_setupNeeded || _setupRunning ? LineHeight * 2 + SectionGap : 0) +
        SectionGap + LineHeight * 2 + SectionGap +
        LineHeight + Rows(_towns.Count) * LineHeight +
        SectionGap + LineHeight + Rows(_dungeons.Count) * LineHeight;

    /// <summary>Rows a spot grid takes; an empty grid still shows its one "none" line.</summary>
    private static int Rows(int spots) => spots == 0 ? 1 : (spots + Columns - 1) / Columns;
}
