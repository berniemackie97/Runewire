using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;

namespace Runewire.Orchestrator.Infrastructure.Preflight;

/// <summary>
/// Checks payload accessibility and architecture hints.
/// On Windows, PE arch is matched to the current process. On Unix, readability and execute bit are verified.
/// ELF and Mach-O are sniffed for architecture hints regardless of platform so we can warn early.
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

        PayloadHeaderInfo header = ReadHeaderInfo(payloadPath);

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            PayloadPreflightResult unixResult = CheckUnix(payloadPath, processArch);
            if (!unixResult.Success)
            {
                return unixResult;
            }
        }

        if (header.Kind == PayloadKind.PE && OperatingSystem.IsWindows())
        {
            PayloadPreflightResult peResult = CheckWindowsPe(payloadPath, processArch, header.Architecture);
            if (!peResult.Success)
            {
                return peResult;
            }
            header = header with { Architecture = peResult.PayloadArchitecture ?? header.Architecture };
        }

        if (!string.IsNullOrWhiteSpace(header.Architecture) &&
            !string.Equals(header.Architecture, "Unknown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(header.Architecture, processArch, StringComparison.OrdinalIgnoreCase))
        {
            return PayloadPreflightResult.Failed(header.Architecture, processArch, new RecipeValidationError("PAYLOAD_ARCH_MISMATCH", $"Payload arch {header.Architecture} does not match process arch {processArch}."));
        }

        return PayloadPreflightResult.Ok(header.Architecture, processArch);
    }

    [SupportedOSPlatform("windows")]
    private static PayloadPreflightResult CheckWindowsPe(string payloadPath, string processArch, string? hintArch)
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
                _ => hintArch ?? "Unknown",
            };

            if (!string.Equals(payloadArch, processArch, StringComparison.OrdinalIgnoreCase) && !string.Equals(payloadArch, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return PayloadPreflightResult.Failed(payloadArch, processArch, new RecipeValidationError("PAYLOAD_ARCH_MISMATCH", $"Payload arch {payloadArch} does not match process arch {processArch}."));
            }

            return PayloadPreflightResult.Ok(payloadArch, processArch);
        }
        catch (BadImageFormatException)
        {
            return PayloadPreflightResult.Ok(hintArch ?? "Unknown", processArch);
        }
        catch (Exception ex)
        {
            return PayloadPreflightResult.Failed(hintArch ?? "Unknown", processArch, new RecipeValidationError("PAYLOAD_READ_FAILED", $"Failed to inspect payload: {ex.Message}"));
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

    private static PayloadHeaderInfo ReadHeaderInfo(string payloadPath)
    {
        try
        {
            using FileStream stream = File.Open(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> header = stackalloc byte[64];
            int read = stream.Read(header);
            header = header[..read];

            if (IsPe(header))
            {
                return new PayloadHeaderInfo(PayloadKind.PE, null);
            }

            if (TryReadElf(header, out string? elfArch))
            {
                return new PayloadHeaderInfo(PayloadKind.ELF, elfArch);
            }

            if (TryReadMach(header, out string? machArch))
            {
                return new PayloadHeaderInfo(PayloadKind.MachO, machArch);
            }
        }
        catch
        {
            // Ignore header read errors; fall through to unknown.
        }

        return new PayloadHeaderInfo(PayloadKind.Unknown, "Unknown");
    }

    private static bool IsPe(ReadOnlySpan<byte> header)
    {
        return header.Length >= 2 && header[0] == (byte)'M' && header[1] == (byte)'Z';
    }

    private static bool TryReadElf(ReadOnlySpan<byte> header, out string? arch)
    {
        arch = null;

        if (header.Length < 20)
        {
            return false;
        }

        if (header[0] != 0x7F || header[1] != (byte)'E' || header[2] != (byte)'L' || header[3] != (byte)'F')
        {
            return false;
        }

        byte eiClass = header[4]; // 1 = 32-bit, 2 = 64-bit

        // e_machine at offset 18-19 (little endian for the common cases we care about)
        ushort eMachine = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(18, 2));

        arch = (eiClass, eMachine) switch
        {
            (1, 3) => "X86",       // EM_386
            (2, 62) => "X64",      // EM_X86_64
            (2, 183) => "ARM64",   // EM_AARCH64
            (1, 40) => "ARM",      // EM_ARM
            _ => "Unknown"
        };

        return true;
    }

    private static bool TryReadMach(ReadOnlySpan<byte> header, out string? arch)
    {
        arch = null;

        if (header.Length < 12)
        {
            return false;
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
        bool isLittleEndian = magic == 0xfeedface || magic == 0xfeedfacf;
        bool isBigEndian = magic == 0xcefaedfe || magic == 0xcffaedfe;

        if (!isLittleEndian && !isBigEndian)
        {
            return false;
        }

        bool is64 = magic == 0xfeedfacf || magic == 0xcffaedfe;

        uint cpuTypeRaw = isLittleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4)) : BinaryPrimitives.ReadUInt32BigEndian(header.Slice(4, 4));

        uint cpuType = cpuTypeRaw & 0x00ffffff; // strip ABI flags

        arch = (cpuType, is64) switch
        {
            (7, false) => "X86",      // CPU_TYPE_X86
            (7, true) => "X64",       // CPU_TYPE_X86_64
            (12, false) => "ARM",     // CPU_TYPE_ARM
            (12, true) => "ARM64",    // CPU_TYPE_ARM64
            _ => "Unknown"
        };

        return true;
    }

    private readonly record struct PayloadHeaderInfo(PayloadKind Kind, string? Architecture);

    private enum PayloadKind
    {
        Unknown = 0,
        PE = 1,
        ELF = 2,
        MachO = 3,
    }
}
