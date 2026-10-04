using System.Reflection;
using Server.Items;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class KitResolverTests
{
    [Fact]
    public void SelectConstructor_FindsAllOptionalConstructorForSpellbook()
    {
        var ctor = KitResolver.SelectConstructor(typeof(Spellbook));

        Assert.NotNull(ctor);
        AssertEveryParameterOptional(ctor);
    }

    [Fact]
    public void SelectConstructor_FindsAllOptionalConstructorForArrow()
    {
        var ctor = KitResolver.SelectConstructor(typeof(Arrow));

        Assert.NotNull(ctor);
        AssertEveryParameterOptional(ctor);
    }

    [Fact]
    public void SelectConstructor_PrefersParameterlessForKatana()
    {
        var ctor = KitResolver.SelectConstructor(typeof(Katana));

        Assert.NotNull(ctor);
        Assert.Empty(ctor.GetParameters());
    }

    private static void AssertEveryParameterOptional(ConstructorInfo ctor)
    {
        foreach (var parameter in ctor.GetParameters())
        {
            Assert.True(parameter.HasDefaultValue);
        }
    }
}
