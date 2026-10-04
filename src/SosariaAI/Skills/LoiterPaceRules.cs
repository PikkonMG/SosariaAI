using System;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// How a person stands about in one place: it stands still for a while, maybe turns to
/// someone near, then strolls a few steps to another free tile inside its home ring and
/// stands again. The old idle walk took one random step on most thinks inside a ring a
/// few tiles wide, which read as a hundred people pacing back and forth in the bank.
/// The stay weight sets how long a stand lasts: a lingerer stands for a minute, a
/// wanderer only for a few seconds. Pure. No world objects.
/// </summary>
public static class LoiterPaceRules
{
    /// <summary>The stay weight of a person who never strolls; it only leaves a packed tile.</summary>
    public const int StandStill = 0;

    public const int SecondsPerStayWeight = 3;
    public const int MinDwellSeconds = 3;

    /// <summary>The longest stand is this many times the shortest one.</summary>
    public const int DwellSpread = 3;

    /// <summary>How often a person who never strolls looks round to see whether its tile got packed.</summary>
    public const int StandStillCheckSeconds = 30;

    /// <summary>A stroll with no ring to keep to stays this close to where it began.</summary>
    public const int FreeStrollRadius = 3;

    /// <summary>Random tiles tried for one stroll before the person just stands on.</summary>
    public const int StrollTries = 4;

    /// <summary>Extra thinks a stroll may take beyond three per tile before it is given up.</summary>
    public const int StrollStepSlack = 4;

    public const int StepsPerTile = 3;

    /// <summary>Out of a hundred, how often a person turns to face someone near when it stops.</summary>
    public const int FacePercent = 60;

    public const int PercentScale = 100;

    /// <summary>
    /// The shortest and longest stay, as a share out of <see cref="PercentScale"/> of the
    /// authored one. Every loiter of one run ended three minutes to the second after it
    /// began, so a corner's crowd left on a beat.
    /// </summary>
    public const int StayMinPercent = 50;

    public const int StayMaxPercent = 150;

    private const int InclusiveSpanPad = 1;

    /// <summary>How long one stand lasts for this stay weight.</summary>
    public static TimeSpan Dwell(int stayWeight, int roll)
    {
        if (stayWeight <= StandStill)
        {
            return TimeSpan.FromSeconds(StandStillCheckSeconds);
        }

        var min = Math.Max(MinDwellSeconds, stayWeight * SecondsPerStayWeight);
        var max = min * DwellSpread;
        return TimeSpan.FromSeconds(min + Math.Abs(roll % (max - min + InclusiveSpanPad)));
    }

    /// <summary>How long one stay lasts: the authored stay, shortened or drawn out by the roll.</summary>
    public static TimeSpan StayLength(TimeSpan authored, int roll)
    {
        var percent = StayMinPercent + Math.Abs(roll % (StayMaxPercent - StayMinPercent + InclusiveSpanPad));
        return TimeSpan.FromTicks(authored.Ticks * percent / PercentScale);
    }

    /// <summary>A person outside its ring walks back in before it does anything else.</summary>
    public static bool MustReturn(Point3D at, Point3D home, int rangeHome) =>
        home != Point3D.Zero && NavMetric.Chebyshev(at, home) > Math.Max(0, rangeHome);

    /// <summary>A person with a zero ring stands on its home tile itself.</summary>
    public static bool PinnedToHome(Point3D home, int rangeHome) => home != Point3D.Zero && rangeHome <= 0;

    /// <summary>
    /// A stroll goal inside the ring: <paramref name="home"/> and <paramref name="rangeHome"/>
    /// when there is a home, else a few tiles round <paramref name="at"/>. Never the tile the
    /// person already stands on.
    /// </summary>
    public static Point3D StrollGoal(Point3D at, Point3D home, int rangeHome, int rollX, int rollY)
    {
        var center = home == Point3D.Zero ? at : home;
        var radius = home == Point3D.Zero ? FreeStrollRadius : Math.Max(1, rangeHome);
        var span = radius * 2 + InclusiveSpanPad;
        var goal = new Point3D(
            center.X + Math.Abs(rollX % span) - radius,
            center.Y + Math.Abs(rollY % span) - radius,
            at.Z
        );

        return goal.X == at.X && goal.Y == at.Y ? new Point3D(at.X + (goal.X < center.X ? 1 : -1), at.Y, at.Z) : goal;
    }

    /// <summary>How many thinks a stroll to <paramref name="goal"/> may take before it is given up.</summary>
    public static int StepBudget(Point3D from, Point3D goal) =>
        NavMetric.Chebyshev(from, goal) * StepsPerTile + StrollStepSlack;

    public static bool ShouldFace(int roll) => Math.Abs(roll % PercentScale) < FacePercent;
}
