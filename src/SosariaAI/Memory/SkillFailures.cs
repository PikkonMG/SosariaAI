using System;
using Server;

namespace SosariaAI.Memory;

/// <summary>
/// One skill's failures since it last succeeded: how many, until when the skill is off
/// the menu, and the spot where it last failed and until when that spot bars it.
/// </summary>
public readonly record struct SkillFailures(int Count, DateTime BlockedUntil, Point3D FailSpot, DateTime FailSpotUntil);
