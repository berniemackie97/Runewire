using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Checks payload accessibility and, on Windows, attempts to match payload architecture to the current process.
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

        if (!OperatingSystem.IsWindows())
        {
            // Architecture check is Windows-only for now.
            return PayloadPreflightResult.Ok(null, processArch);
        }

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
}
