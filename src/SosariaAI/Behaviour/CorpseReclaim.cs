namespace SosariaAI.Behaviour;

public enum CorpseReclaimResult
{
    Exists,
    Reclaimed,
    Decayed,
    Stripped
}

public static class CorpseReclaim
{
    public static CorpseReclaimResult Decide(bool corpseExists, bool ownerIsSelf, int remainingItems)
    {
        if (!corpseExists)
        {
            return CorpseReclaimResult.Decayed;
        }

        if (!ownerIsSelf)
        {
            return CorpseReclaimResult.Decayed;
        }

        if (remainingItems <= 0)
        {
            return CorpseReclaimResult.Stripped;
        }

        return CorpseReclaimResult.Exists;
    }

    /// <summary>
    /// True when a person who dies again goes back for its earlier body, still lying in the world
    /// as its own, not the new one: the earlier body holds more of its gear (the pieces it wore,
    /// then everything). A red that died naked on the way back walked to the empty body of
    /// that second death and left the one with its gear to rot.
    /// </summary>
    public static bool KeepsEarlierBody(int earlierWorn, int earlierItems, int newWorn, int newItems) =>
        earlierWorn != newWorn ? earlierWorn > newWorn : earlierItems > newItems;

    public static bool CanLoot(CorpseReclaimResult result) =>
        result == CorpseReclaimResult.Exists;

    public static CorpseReclaimResult AfterLoot(int remainingItems) =>
        remainingItems <= 0 ? CorpseReclaimResult.Reclaimed : CorpseReclaimResult.Exists;
}
