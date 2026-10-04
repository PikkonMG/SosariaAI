using SosariaAI.Population;
using Xunit;

namespace SosariaAI.Tests;

public class LiveProofTests
{
    private const int BootDelaySeconds = 15;

    [Fact]
    public void BootStart_WaitsNamedSeconds() =>
        Assert.Equal(BootDelaySeconds, LiveProof.StartDelaySeconds);
}
