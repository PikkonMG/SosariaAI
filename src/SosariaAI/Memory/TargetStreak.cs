using System;
using Server;

namespace SosariaAI.Memory;

/// <summary>What one job aims at: a key that names it, and its place when it has one.</summary>
public readonly record struct JobTarget(string Key, Point3D Spot);

/// <summary>
/// One job's failures at one target since it last worked there: how many in a row failed the
/// same way, that way, and until when the job rests for that target.
/// </summary>
public readonly record struct TargetStreak(int Count, string Reason, DateTime RestUntil, Point3D Spot);
