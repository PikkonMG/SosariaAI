using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Logging;

namespace SosariaAI.Behaviour;

/// <summary>
/// Removes dead equipment references from saved corpses before a client can see them.
/// ModernUO's corpse packet assumes every entry is a live Item.
/// </summary>
public static class CorpseSanitizer
{
    private static readonly ILogger logger = SosariaLog.For(typeof(CorpseSanitizer));
    private static readonly PropertyInfo equipItemsProperty = typeof(Corpse).GetProperty(
        nameof(Corpse.EquipItems),
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
    );

    public static int CleanWorld()
    {
        var corpseCount = 0;
        var repaired = 0;

        foreach (var item in World.Items.Values)
        {
            if (item is Corpse corpse)
            {
                corpseCount++;
                repaired += Clean(corpse);
            }
        }

        if (repaired > 0)
        {
            logger.Warning("SosariaAI checked {CorpseCount} corpses and repaired {Count} corpse references", corpseCount, repaired);
        }
        else
        {
            logger.Information("SosariaAI checked {CorpseCount} corpses; no corpse repair was needed", corpseCount);
        }

        return repaired;
    }

    public static int Clean(Corpse corpse)
    {
        var items = corpse?.EquipItems;

        if (items == null)
        {
            equipItemsProperty?.SetValue(corpse, new List<Item>());
            return 1;
        }

        if (items.Count == 0)
        {
            return 0;
        }

        var before = items.Count;
        items.RemoveAll(item => Invalid(item));
        return before - items.Count;
    }

    public static bool Invalid(Item item) => item == null || item.Deleted;
}
