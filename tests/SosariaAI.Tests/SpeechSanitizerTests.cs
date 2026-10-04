using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class SpeechSanitizerTests
{
    [Fact]
    public void StripDashes_RemovesEmAndEnDashes()
    {
        var raw = "Fine timber today" + SpeechSanitizer.EmDash + "I'm near a full load.";
        var clean = SpeechSanitizer.StripDashes(raw);

        Assert.DoesNotContain(SpeechSanitizer.EmDash, clean);
        Assert.DoesNotContain(SpeechSanitizer.EnDash, clean);
        Assert.Contains(SpeechSanitizer.Hyphen, clean);
    }

    [Fact]
    public void StripDashes_LeavesPlainText()
    {
        Assert.Equal("Hello there.", SpeechSanitizer.StripDashes("Hello there."));
        Assert.Null(SpeechSanitizer.StripDashes(null));
    }
}
