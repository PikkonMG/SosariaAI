using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class InterruptWindowTests
{
    private const long Start = 100_000;
    private const long OneSecond = 1000;
    private const int Both = 2;
    private const int One = 1;
    private const int None = 0;

    [Fact]
    public void Count_OnlyTheRecentOnes()
    {
        var window = new InterruptWindow();
        window.Note(Start);
        window.Note(Start + OneSecond);

        Assert.Equal(Both, window.Count(Start + OneSecond));
        Assert.Equal(One, window.Count(Start + InterruptWindow.WindowMs));
        Assert.Equal(None, window.Count(Start + OneSecond + InterruptWindow.WindowMs));
    }

    [Fact]
    public void Note_PastCapacity_KeepsTheNewest()
    {
        var window = new InterruptWindow();

        for (var i = 0; i < InterruptWindow.Capacity * Both; i++)
        {
            window.Note(Start + i);
        }

        Assert.Equal(InterruptWindow.Capacity, window.Count(Start + InterruptWindow.Capacity * Both));
    }

    [Fact]
    public void Clear_ForgetsAll()
    {
        var window = new InterruptWindow();
        window.Note(Start);
        window.Clear();

        Assert.Equal(None, window.Count(Start));
    }
}
