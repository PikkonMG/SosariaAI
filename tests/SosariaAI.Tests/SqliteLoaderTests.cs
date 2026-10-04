using System;
using System.Reflection;
using System.Runtime.Loader;
using SosariaAI.Memory;
using Xunit;

namespace SosariaAI.Tests;

public class SqliteLoaderTests
{
    // Microsoft.Data.Sqlite asks for this one by short name: no version, no key.
    private const string BatteriesShortName = "SQLitePCLRaw.batteries_v2";

    [Theory]
    [InlineData("Microsoft.Data.Sqlite", true)]
    [InlineData("SQLitePCLRaw.core", true)]
    [InlineData(BatteriesShortName, true)]
    [InlineData("Server", false)]
    [InlineData("UOContent", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSqliteAssembly_OnlyTheSqliteFamily(string shortName, bool expected) =>
        Assert.Equal(expected, SqliteLoader.IsSqliteAssembly(shortName));

    [Fact]
    public void ResolveManaged_AnswersAShortNameWithTheLoadedAssembly()
    {
        var batteries = typeof(SQLitePCL.Batteries_V2).Assembly;

        var resolved = SqliteLoader.ResolveManaged(
            AssemblyLoadContext.Default,
            new AssemblyName(BatteriesShortName),
            AppContext.BaseDirectory
        );

        Assert.Same(batteries, resolved);
    }

    [Fact]
    public void ResolveManaged_LeavesOtherAssembliesToModernUO() =>
        Assert.Null(SqliteLoader.ResolveManaged(AssemblyLoadContext.Default, new AssemblyName("Server"), AppContext.BaseDirectory));
}
