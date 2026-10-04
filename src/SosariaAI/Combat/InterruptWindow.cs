namespace SosariaAI.Combat;

/// <summary>
/// The casts a character lost to blows lately. A mage that keeps losing its words changes
/// how it fights (a ward, first circle spells, more distance); the count is also what Jev reads.
/// </summary>
public sealed class InterruptWindow
{
    /// <summary>Only interruptions this recent count.</summary>
    public const int WindowMs = 10000;

    /// <summary>The window remembers this many; more than that reads as "again and again" anyway.</summary>
    public const int Capacity = 8;

    private readonly long[] _at = new long[Capacity];
    private int _next;
    private int _stored;

    public void Note(long now)
    {
        _at[_next] = now;
        _next = (_next + 1) % Capacity;

        if (_stored < Capacity)
        {
            _stored++;
        }
    }

    public int Count(long now)
    {
        var count = 0;

        for (var i = 0; i < _stored; i++)
        {
            if (now - _at[i] < WindowMs)
            {
                count++;
            }
        }

        return count;
    }

    public void Clear()
    {
        _next = 0;
        _stored = 0;
    }
}
