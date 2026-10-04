using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Tests;

/// <summary>Hands a person a routine the way the tests need it.</summary>
internal static class TestRoutine
{
    /// <summary>
    /// Sets the routine without <see cref="SosariaCharacter.AttachRoutine"/>: that dresses the
    /// character for its role, and the test world has no skill table.
    /// </summary>
    internal static void Give(SosariaCharacter character, Routine routine)
    {
        typeof(SosariaCharacter).GetProperty(nameof(SosariaCharacter.Routine))!.SetValue(character, routine);
        routine.Bind(character);
    }
}
