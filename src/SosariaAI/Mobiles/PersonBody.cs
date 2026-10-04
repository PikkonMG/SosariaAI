namespace SosariaAI.Mobiles;

/// <summary>The size of a standing person, as the engine measures one.</summary>
public static class PersonBody
{
    /// <summary>
    /// Height a standing person fills above its floor: the engine's movement check, its
    /// raise fit test and its house lookup all use this span.
    /// </summary>
    public const int Height = 16;
}
