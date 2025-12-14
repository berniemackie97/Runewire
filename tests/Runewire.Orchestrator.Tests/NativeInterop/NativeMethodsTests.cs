using Runewire.Orchestrator.Infrastructure.NativeInterop;

namespace Runewire.Orchestrator.Tests.NativeInterop;

public sealed class NativeMethodsTests
{
    [Fact]
    public void GetLibraryFileName_returns_platform_specific_name()
    {
        string fileName = NativeMethods.GetLibraryFileName();

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("Runewire.Injector.dll", fileName);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("libRunewire.Injector.dylib", fileName);
        }
        else
        {
            Assert.Equal("libRunewire.Injector.so", fileName);
        }
    }

    [Fact]
    public void GetPreferredLibraryPath_uses_env_path_when_set()
    {
        string expected = Path.Combine(Path.GetTempPath(), "custom", "Runewire.Injector.dll");
        Environment.SetEnvironmentVariable("RUNEWIRE_INJECTOR_PATH", expected);
        try
        {
            string? path = NativeMethods.GetPreferredLibraryPath();
            Assert.Equal(expected, path);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RUNEWIRE_INJECTOR_PATH", null);
        }
    }
}
