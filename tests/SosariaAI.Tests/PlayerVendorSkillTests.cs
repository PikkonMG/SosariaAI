using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class PlayerVendorSkillTests
{
    [Fact]
    public void SkillKind_IsStable() =>
        Assert.Equal("PlayerVendor", SkillKinds.PlayerVendor);
}
