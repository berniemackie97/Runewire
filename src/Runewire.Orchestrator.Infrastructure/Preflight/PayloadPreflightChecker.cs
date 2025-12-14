using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Checks payload accessibility and, on Windows, attempts to match payload architecture to the current process.
/// On Unix, checks readability and execute bit.
/// </summary>
public sealed class PayloadPreflightChecker : IPayloadPreflightChecker
{
    public PayloadPreflightResult Check(RunewireRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        string payloadPath = recipe.PayloadPath ?? string.Empty;
        string processArch = RuntimeInformation.ProcessArchitecture.ToString();

        if (!File.Exists(payloadPath))
        {
            return PayloadPreflightResult.Failed(null, processArch, new RecipeValidationError("PAYLOAD_PATH_NOT_FOUND", $"Payload file not found: {payloadPath}"));
        }

        if (OperatingSystem.IsWindows())
        {
            return CheckWindows(payloadPath, processArch);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return CheckUnix(payloadPath, processArch);
        }

        return PayloadPreflightResult.Ok("Unknown", processArch);
    }

    [SupportedOSPlatform("windows")]
    private static PayloadPreflightResult CheckWindows(string payloadPath, string processArch)
    {
        try
        {
            using FileStream stream = File.Open(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using PEReader peReader = new(stream, PEStreamOptions.Default);

            string payloadArch = peReader.PEHeaders.CoffHeader.Machine switch
            {
                Machine.Amd64 => "X64",
                Machine.I386 => "X86",
                Machine.Arm64 => "ARM64",
                Machine.Arm => "ARM",
                _ => "Unknown",
            };

            if (payloadArch == "Unknown")
            {
                return PayloadPreflightResult.Ok(payloadArch, processArch);
            }

            if (!string.Equals(payloadArch, processArch, StringComparison.OrdinalIgnoreCase))
            {
                return PayloadPreflightResult.Failed(payloadArch, processArch, new RecipeValidationError("PAYLOAD_ARCH_MISMATCH", $"Payload arch {payloadArch} does not match process arch {processArch}."));
            }

            return PayloadPreflightResult.Ok(payloadArch, processArch);
        }
        catch (BadImageFormatException)
        {
            return PayloadPreflightResult.Ok("Unknown", processArch);
        }
        catch (Exception ex)
        {
            return PayloadPreflightResult.Failed("Unknown", processArch, new RecipeValidationError("PAYLOAD_READ_FAILED", $"Failed to inspect payload: {ex.Message}"));
        }
    }

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private static PayloadPreflightResult CheckUnix(string payloadPath, string processArch)
    {
        try
        {
            using FileStream stream = File.Open(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            stream.ReadByte(); // touch the stream to verify readability
        }
        catch (Exception ex)
        {
            return PayloadPreflightResult.Failed("Unknown", processArch, new RecipeValidationError("PAYLOAD_READ_FAILED", $"Failed to inspect payload: {ex.Message}"));
        }

        try
        {
            UnixFileMode mode = File.GetUnixFileMode(payloadPath);
            bool hasExec = mode.HasFlag(UnixFileMode.UserExecute) || mode.HasFlag(UnixFileMode.GroupExecute) || mode.HasFlag(UnixFileMode.OtherExecute);
            if (!hasExec)
            {
                return PayloadPreflightResult.Failed("Unknown", processArch, new RecipeValidationError("PAYLOAD_EXEC_PERMISSION_MISSING", $"Payload is not marked executable: {payloadPath}"));
            }
        }
        catch (PlatformNotSupportedException)
        {
            // Ignore, fall through as best-effort.
        }

        return PayloadPreflightResult.Ok("Unknown", processArch);
    }
}
