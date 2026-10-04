using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading;

namespace SosariaAI.Memory;

/// <summary>
/// Loads SQLite from the plugin folder. ModernUO loads the plugin into the default load
/// context, and two things then go wrong without help:
/// <list type="bullet">
/// <item>Microsoft.Data.Sqlite asks for <c>SQLitePCLRaw.batteries_v2</c> by its short name.
/// ModernUO's own resolver accepts only an exact full name and throws, so the connection type
/// fails to start. A resolver on the default load context runs before ModernUO's and answers
/// for the SQLite assemblies by short name.</item>
/// <item>The default load context does not search the plugin's runtimes folder for the native
/// library, so the SQLite provider gets a resolver that looks in runtimes/&lt;rid&gt;/native/.</item>
/// </list>
/// </summary>
public static class SqliteLoader
{
    /// <summary>The native library name the SQLite provider imports.</summary>
    public const string LibraryName = "e_sqlite3";

    /// <summary>The short names of the managed SQLite assemblies start with one of these.</summary>
    public static readonly string[] ManagedPrefixes = ["Microsoft.Data.Sqlite", "SQLitePCLRaw."];

    private const string RuntimesFolder = "runtimes";
    private const string NativeFolder = "native";
    private const string UnixPrefix = "lib";

    // A managed assembly file, and the native library on Windows.
    private const string DllExtension = ".dll";
    private const string LinuxExtension = ".so";
    private const string MacExtension = ".dylib";

    private static int _registered;

    /// <summary>
    /// Registers both resolvers once, before the first connection. A second call does nothing.
    /// </summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        var pluginFolder = Path.GetDirectoryName(typeof(SqliteLoader).Assembly.Location) ?? AppContext.BaseDirectory;
        AssemblyLoadContext.Default.Resolving += (context, name) => ResolveManaged(context, name, pluginFolder);
        NativeLibrary.SetDllImportResolver(
            typeof(SQLitePCL.SQLite3Provider_e_sqlite3).Assembly,
            (name, _, _) => ResolveNative(pluginFolder, name)
        );
    }

    /// <summary>True when the short name is one of the managed SQLite assemblies.</summary>
    public static bool IsSqliteAssembly(string shortName)
    {
        if (string.IsNullOrEmpty(shortName))
        {
            return false;
        }

        for (var i = 0; i < ManagedPrefixes.Length; i++)
        {
            if (shortName.StartsWith(ManagedPrefixes[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A managed SQLite assembly by short name: the copy already loaded, else the file in the
    /// plugin folder. Null for any other assembly, so every other request goes on as before.
    /// </summary>
    internal static Assembly ResolveManaged(AssemblyLoadContext context, AssemblyName name, string pluginFolder)
    {
        if (!IsSqliteAssembly(name?.Name))
        {
            return null;
        }

        var loaded = AppDomain.CurrentDomain.GetAssemblies();

        for (var i = 0; i < loaded.Length; i++)
        {
            if (string.Equals(loaded[i].GetName().Name, name.Name, StringComparison.Ordinal))
            {
                return loaded[i];
            }
        }

        var path = Path.Combine(pluginFolder, name.Name + DllExtension);
        return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    }

    /// <summary>
    /// The library from the plugin folder, or no handle so the default search still runs
    /// (a test host finds the library through its own deps file).
    /// </summary>
    private static IntPtr ResolveNative(string pluginFolder, string name)
    {
        if (!string.Equals(name, LibraryName, StringComparison.Ordinal) || LibraryPath(pluginFolder) is not { } path)
        {
            return IntPtr.Zero;
        }

        return File.Exists(path) ? NativeLibrary.Load(path) : IntPtr.Zero;
    }

    /// <summary>runtimes/&lt;rid&gt;/native/&lt;file&gt; under the plugin folder, or null on an unknown platform.</summary>
    private static string LibraryPath(string pluginFolder)
    {
        var runtimeId = RuntimeId();
        var fileName = FileName();

        if (runtimeId == null || fileName == null)
        {
            return null;
        }

        return Path.Combine(pluginFolder, RuntimesFolder, runtimeId, NativeFolder, fileName);
    }

    /// <summary>The NuGet runtime id of this process, such as linux-x64 or win-arm64, or null.</summary>
    private static string RuntimeId()
    {
        var os = OperatingSystemPart();
        var architecture = ArchitecturePart(RuntimeInformation.ProcessArchitecture);
        return os == null || architecture == null ? null : os + "-" + architecture;
    }

    /// <summary>The native file name on this OS, or null on an unknown OS.</summary>
    private static string FileName()
    {
        if (OperatingSystem.IsWindows())
        {
            return LibraryName + DllExtension;
        }

        if (OperatingSystem.IsMacOS())
        {
            return UnixPrefix + LibraryName + MacExtension;
        }

        return OperatingSystem.IsLinux() ? UnixPrefix + LibraryName + LinuxExtension : null;
    }

    private static string OperatingSystemPart()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        return OperatingSystem.IsLinux() ? "linux" : null;
    }

    private static string ArchitecturePart(Architecture architecture) =>
        architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => null
        };
}
