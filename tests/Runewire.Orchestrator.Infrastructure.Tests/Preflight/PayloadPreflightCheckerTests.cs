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

    [Fact]
    public void Check_detects_elf_arch_and_mismatch_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // processArch mapping in tests assumes Windows x64
        }

        string tempPayload = Path.Combine(Path.GetTempPath(), $"runewire-payload-{Guid.NewGuid():N}.elf");

        // Minimal ELF header with EM_AARCH64 (183) and 64-bit class.
        byte[] elfHeader =
        [
            0x7F, (byte)'E', (byte)'L', (byte)'F', // magic
            0x02, // EI_CLASS = 64-bit
            0x01, // EI_DATA = little endian
            0x01, // EI_VERSION
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // padding to e_machine
            0xB7, 0x00 // e_machine = 183 (AARCH64) little endian
        ];
        File.WriteAllBytes(tempPayload, elfHeader);

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
            Assert.Contains(result.Errors, e => e.Code == "PAYLOAD_ARCH_MISMATCH");
        }
        finally
        {
            try { File.Delete(tempPayload); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Check_detects_macho_arch_hint()
    {
        string tempPayload = Path.Combine(Path.GetTempPath(), $"runewire-payload-{Guid.NewGuid():N}.macho");

        // Minimal Mach-O header: magic = 0xfeedfacf (64-bit little), cputype = CPU_TYPE_X86_64 (0x01000007)
        byte[] machHeader =
        [
            0xcf, 0xfa, 0xed, 0xfe, // magic (little endian feedfacf)
            0x07, 0x00, 0x00, 0x01, // cputype with ABI flag
            0x00, 0x00, 0x00, 0x00  // cpusubtype (ignored)
        ];
        File.WriteAllBytes(tempPayload, machHeader);

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
            Assert.Equal("X64", result.PayloadArchitecture);
        }
        finally
        {
            try { File.Delete(tempPayload); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Check_validates_step_payloads()
    {
        // Primary payload exists, step payload is missing -> should fail on step payload.
        using Process current = Process.GetCurrentProcess();
        string primaryPayload = Path.Combine(Path.GetTempPath(), $"runewire-payload-{Guid.NewGuid():N}.exe");
        File.Copy(current.MainModule!.FileName!, primaryPayload);

        string missingStepPayload = Path.Combine(Path.GetTempPath(), $"runewire-step-{Guid.NewGuid():N}.dll");

        RunewireRecipe recipe = new(
            "demo",
            null,
            RecipeTarget.Self(),
            new InjectionTechnique("CreateRemoteThread"),
            primaryPayload,
            RequireInteractiveConsent: false,
            AllowKernelDrivers: false,
            Steps: new List<RecipeStep> { RecipeStep.Inject("StepTech", missingStepPayload) });

        PayloadPreflightChecker checker = new();

        try
        {
            PayloadPreflightResult result = checker.Check(recipe);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Code == "PAYLOAD_PATH_NOT_FOUND");
        }
        finally
        {
            try { File.Delete(primaryPayload); } catch { /* ignore */ }
        }
    }
}
