using System;

namespace SosariaAI.Behaviour;

/// <summary>Where a beat is said: aloud, or in the speaker's party chat.</summary>
public enum SceneChannel
{
    Say,
    Party
}

/// <summary>
/// One line of a scene: said by cast member <see cref="Actor"/> (0 is the one the scene is
/// about) <see cref="Delay"/> after the beat before it.
/// </summary>
public readonly record struct SceneBeat(TimeSpan Delay, int Actor, string Text, SceneChannel Channel);
