using System.Diagnostics;
using System.Runtime.InteropServices;
using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Infrastructure.Preflight;

namespace Runewire.Orchestrator.Infrastructure.Tests.Preflight;

public sealed class PayloadPreflightCheckerTests
{
    [Fact]
    public void Check_fails_when_payload_not_found()
    {
        RunewireRecipe recipe = new(
            "demo",
            null,
            RecipeTarget.Self(),
            new InjectionTechnique("CreateRemoteThread"),
            @"C:\missing\nofile.dll",
            RequireInteractiveConsent: false,
            AllowKernelDrivers: false);

        PayloadPreflightChecker checker = new();

        PayloadPreflightResult result = checker.Check(recipe);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == "PAYLOAD_PATH_NOT_FOUND");
    }

    [Fact]
    public void Check_succeeds_for_current_process_binary_copy()
    {
        using Process current = Process.GetCurrentProcess();
        string currentPath = current.MainModule!.FileName!;

        string tempPayload = Path.Combine(Path.GetTempPath(), $"runewire-payload-{Guid.NewGuid():N}.exe");
        File.Copy(currentPath, tempPayload);

        RunewireRecipe recipe = new(
            "demo",
            null,
            RecipeTarget.Self(),
            new InjectionTechnique("CreateRemoteThread"),
            tempPayload,
            RequireInteractiveConsent: false,
            AllowKernelDrivers: false);

        PayloadPreflightChecker checker = new();

        try
        {
            PayloadPreflightResult result = checker.Check(recipe);

            Assert.True(result.Success);
            Assert.Equal(RuntimeInformation.ProcessArchitecture.ToString(), result.ProcessArchitecture);
        }
        finally
        {
            try { File.Delete(tempPayload); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Check_returns_unknown_for_non_pe_file()
    {
        string tempPayload = Path.Combine(Path.GetTempPath(), $"runewire-payload-{Guid.NewGuid():N}.bin");
        File.WriteAllText(tempPayload, "not-a-pe");

        RunewireRecipe recipe = new(
            "demo",
            null,
            RecipeTarget.Self(),
            new InjectionTechnique("CreateRemoteThread"),
            tempPayload,
            RequireInteractiveConsent: false,
            AllowKernelDrivers: false);

        PayloadPreflightChecker checker = new();

        try
        {
            PayloadPreflightResult result = checker.Check(recipe);

            Assert.True(result.Success);
            Assert.Equal("Unknown", result.PayloadArchitecture);
        }
        finally
        {
            try { File.Delete(tempPayload); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Check_on_unix_without_exec_bit_fails()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // exec bit check is Unix-only
        }

        string tempPayload = Path.Combine(Path.GetTempPath(), $"runewire-payload-{Guid.NewGuid():N}.bin");
        File.WriteAllText(tempPayload, "not-a-pe");

        // 0644 -> no execute bits
        File.SetUnixFileMode(tempPayload, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        RunewireRecipe recipe = new(
            "demo",
            null,
            RecipeTarget.Self(),
            new InjectionTechnique("CreateRemoteThread"),
            tempPayload,
            RequireInteractiveConsent: false,
            AllowKernelDrivers: false);

        PayloadPreflightChecker checker = new();

        try
        {
            PayloadPreflightResult result = checker.Check(recipe);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Code == "PAYLOAD_EXEC_PERMISSION_MISSING");
        }
        finally
        {
            try { File.Delete(tempPayload); } catch { /* ignore */ }
        }
    }
}
