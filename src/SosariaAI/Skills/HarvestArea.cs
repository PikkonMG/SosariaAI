using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Skills;

/// <summary>
/// Where a worker looks for wood, ore or fish. The home patch first, then ever wider
/// rings around it. A patch runs dry fast: the Britain woodcutters' forest holds 32 trees,
/// four woodcutters empty it in minutes, and a tree takes 20 to 40 minutes to grow back.
/// A real woodcutter walks deeper into the woods instead of going home empty handed.
/// </summary>
public static class HarvestArea
{
    /// <summary>Tiles added on every side for each wider ring.</summary>
    public const int WidenStep = 24;

    /// <summary>How many wider rings are tried after the home patch.</summary>
    public const int MaxWidenings = 1;

    /// <summary>
    /// The Britain mine sits on the mountain. A wider ring walks north into troll
    /// and ettin woods.
    /// </summary>
    public const int MineWidenings = 0;

    public static IReadOnlyList<Rectangle2D> SearchOrder(Rectangle2D home, int maxWidenings)
    {
        var widenings = Math.Max(0, maxWidenings);
        var areas = new List<Rectangle2D>(widenings + 1) { home };

        for (var ring = 1; ring <= widenings; ring++)
        {
            var margin = ring * WidenStep;
            areas.Add(
                new Rectangle2D(
                    home.X - margin,
                    home.Y - margin,
                    home.Width + margin * 2,
                    home.Height + margin * 2
                )
            );
        }

        return areas;
    }
}
