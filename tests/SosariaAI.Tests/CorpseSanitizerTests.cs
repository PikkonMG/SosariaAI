using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class CorpseSanitizerTests
{
    [Fact]
    public void Invalid_NullItem_IsStale() => Assert.True(CorpseSanitizer.Invalid(null));
}
