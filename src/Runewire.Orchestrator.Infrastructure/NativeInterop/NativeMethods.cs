using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Runewire.Orchestrator.Infrastructure.NativeInterop;

/// <summary>
/// P/Invoke surface for the native injector.
/// </summary>
internal static partial class NativeMethods
{
    // Base name so the runtime can resolve the platform-specific file later.
    private const string DllName = "Runewire.Injector";

    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, ResolveLibrary);
    }

    /// <summary>
    /// rw_inject entrypoint.
    /// </summary>
    [LibraryImport(DllName, EntryPoint = "rw_inject")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial int RwInject(in RwInjectionRequest request, out RwInjectionResult result);

    internal static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // Only handle our injector; let the runtime handle other imports.
        if (!string.Equals(libraryName, DllName, StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        string? preferredPath = GetPreferredLibraryPath();
        if (string.IsNullOrWhiteSpace(preferredPath))
        {
            return IntPtr.Zero;
        }

        if (!File.Exists(preferredPath))
        {
            return IntPtr.Zero;
        }

        return NativeLibrary.Load(preferredPath, assembly, searchPath);
    }

    internal static string? GetPreferredLibraryPath()
    {
        string fileName = GetLibraryFileName();

        string? explicitPath = Environment.GetEnvironmentVariable("RUNEWIRE_INJECTOR_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        string? dir = Environment.GetEnvironmentVariable("RUNEWIRE_INJECTOR_DIR");
        if (!string.IsNullOrWhiteSpace(dir))
        {
            return Path.Combine(dir, fileName);
        }

        return Path.Combine(AppContext.BaseDirectory, fileName);
    }

    internal static string GetLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
        {
            return $"{DllName}.dll";
        }

        if (OperatingSystem.IsMacOS())
        {
            return $"lib{DllName}.dylib";
        }

        return $"lib{DllName}.so";
    }
}
