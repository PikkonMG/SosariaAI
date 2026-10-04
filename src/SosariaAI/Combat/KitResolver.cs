using System;
using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Logging;

namespace SosariaAI.Combat;

/// <summary>
/// Builds one kit item from a type name. Some item types (Spellbook, Arrow) have no true
/// parameterless constructor: every parameter is optional. Reflection's parameterless
/// create fails on those, so we pick the smallest constructor whose parameters all have
/// defaults and invoke it with those defaults. Any failure returns null and is logged by
/// the caller; a bad kit entry must never crash the server.
/// </summary>
public static class KitResolver
{
    private static readonly ILogger logger = SosariaLog.For(typeof(KitResolver));

    public static Item Create(string typeName, Func<string, Type> findType)
    {
        if (string.IsNullOrWhiteSpace(typeName) || findType == null)
        {
            return null;
        }

        var type = findType(typeName);

        if (type == null || type.IsAbstract || !typeof(Item).IsAssignableFrom(type))
        {
            return null;
        }

        Item item;

        try
        {
            if (Construct(type) is not Item built)
            {
                return null;
            }

            item = built;
        }
        catch (Exception e)
        {
            logger.Warning(e, "Kit type {Type} could not be constructed", typeName);
            return null;
        }

        if (item is Arrow arrow)
        {
            arrow.Amount = SosariaCombat.ArrowTopUpCount;
        }

        if (item is BaseReagent reagent)
        {
            reagent.Amount = SosariaCombat.ReagentTopUpCount;
        }

        if (item is Spellbook book)
        {
            book.Content = ulong.MaxValue;
        }

        return item;
    }

    private static object Construct(Type type)
    {
        var ctor = SelectConstructor(type);

        if (ctor == null)
        {
            return null;
        }

        var chosen = ctor.GetParameters();
        var arguments = new object[chosen.Length];

        for (var i = 0; i < chosen.Length; i++)
        {
            arguments[i] = Coerce(chosen[i].DefaultValue, chosen[i].ParameterType);
        }

        return ctor.Invoke(arguments);
    }

    // Prefer a public parameterless constructor. Otherwise the smallest constructor whose
    // every parameter is optional (Spellbook and Arrow only have all-optional ones).
    internal static ConstructorInfo SelectConstructor(Type type)
    {
        ConstructorInfo best = null;

        foreach (var ctor in type.GetConstructors())
        {
            var parameters = ctor.GetParameters();

            if (parameters.Length == 0)
            {
                return ctor;
            }

            if (!AllOptional(parameters))
            {
                continue;
            }

            if (best == null || parameters.Length < best.GetParameters().Length)
            {
                best = ctor;
            }
        }

        return best;
    }

    private static bool AllOptional(ParameterInfo[] parameters)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (!parameters[i].HasDefaultValue)
            {
                return false;
            }
        }

        return true;
    }

    private static object Coerce(object value, Type target)
    {
        if (value == null || value == DBNull.Value)
        {
            return target.IsValueType ? Activator.CreateInstance(target) : null;
        }

        if (target.IsInstanceOfType(value) || target.IsEnum)
        {
            return value;
        }

        return Convert.ChangeType(value, target);
    }

    public static bool IsKitType(Item item, IReadOnlyList<string> kit, Func<string, Type> findType)
    {
        if (item == null || kit == null || findType == null)
        {
            return false;
        }

        for (var i = 0; i < kit.Count; i++)
        {
            var type = findType(kit[i]);

            if (type != null && type.IsInstanceOfType(item))
            {
                return true;
            }
        }

        return false;
    }
}
