using System;

namespace SosariaAI.Spawning;

/// <summary>
/// Temper, rolled in opposed pairs so nobody is both brave and cautious. A person may
/// hold none of a pair.
/// </summary>
[Flags]
public enum PersonTrait
{
    None = 0,
    Brave = 1 << 0,
    Cautious = 1 << 1,
    Restless = 1 << 2,
    Homebody = 1 << 3,
    Social = 1 << 4,
    Loner = 1 << 5,
    Greedy = 1 << 6,
    Generous = 1 << 7
}
