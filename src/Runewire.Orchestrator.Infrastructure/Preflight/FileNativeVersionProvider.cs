using System.Diagnostics;
using Runewire.Orchestrator.Infrastructure.NativeInterop;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Reads the native injector version from the resolved library path.
/// </summary>
public sealed class FileNativeVersionProvider : INativeVersionProvider
{
    private readonly Lazy<Version?> _version;

    public FileNativeVersionProvider()
    {
        _version = new Lazy<Version?>(ReadVersion, isThreadSafe: true);
    }

    public Version? CurrentNativeVersion => _version.Value;

    private static Version? ReadVersion()
    {
        string? path = NativeMethods.GetPreferredLibraryPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
            return !string.IsNullOrWhiteSpace(info.FileVersion) ? new Version(info.FileVersion) : null;
        }
        catch
        {
            return null;
        }
    }
}
