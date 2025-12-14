using System.Runtime.InteropServices;
using System.Text.Json;
using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Infrastructure.NativeInterop;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.InjectionEngines;

/// <summary>
/// Injection engine that calls into the native injector through INativeInjectorInvoker.
/// </summary>
public sealed class NativeInjectionEngine : IInjectionEngine
{
    private readonly INativeInjectorInvoker _invoker;

    /// <summary>
    /// Uses the default native DLL invoker.
    /// </summary>
    public NativeInjectionEngine() : this(new NativeInjectorInvoker())
    {
    }

    /// <summary>
    /// Internal so tests and advanced hosts can swap the invoker.
    /// </summary>
    internal NativeInjectionEngine(INativeInjectorInvoker invoker)
    {
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    }

    public Task<InjectionResult> ExecuteAsync(InjectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset started = DateTimeOffset.UtcNow;

        // Track unmanaged allocations so I can free everything in finally.
        List<IntPtr> allocations = new(capacity: 6);
        RwInjectionRequest nativeRequest;

        try
        {
            nativeRequest = MapToNativeRequest(request, allocations);

            RwInjectionResult nativeResult;
            int status;

            try
            {
                status = _invoker.Inject(in nativeRequest, out nativeResult);
            }
            catch (Exception ex)
            {
                DateTimeOffset completed = DateTimeOffset.UtcNow;
                return Task.FromResult(InjectionResult.Failed("NATIVE_INVOKE_FAILED", ex.Message, started, completed));
            }

            InjectionResult managedResult = MapFromNativeResult(status, nativeResult, started);
            return Task.FromResult(managedResult);
        }
        finally
        {
            foreach (IntPtr ptr in allocations)
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }
    }

    /// <summary>
    /// Build the unmanaged request struct and record every allocation that needs to be freed.
    /// </summary>
    private static RwInjectionRequest MapToNativeRequest(InjectionRequest request, List<IntPtr> allocations)
    {
        ArgumentNullException.ThrowIfNull(allocations);

        IntPtr Alloc(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return IntPtr.Zero;
            }

            IntPtr ptr = Marshal.StringToHGlobalAnsi(value);
            allocations.Add(ptr);
            return ptr;
        }

        RwTarget target = MapTarget(request.Target, Alloc);
        string? parametersJson = request.TechniqueParameters is { Count: > 0 }
            ? JsonSerializer.Serialize(request.TechniqueParameters, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            : null;

        return new RwInjectionRequest
        {
            RecipeName = Alloc(request.RecipeName),
            RecipeDescription = Alloc(request.RecipeDescription),
            Target = target,
            TechniqueName = Alloc(request.TechniqueName),
            TechniqueParametersJson = Alloc(parametersJson),
            PayloadPath = Alloc(request.PayloadPath),
            AllowKernelDrivers = request.AllowKernelDrivers ? 1 : 0,
            RequireInteractiveConsent = request.RequireInteractiveConsent ? 1 : 0,
        };
    }

    /// <summary>
    /// Convert RecipeTarget into the native target struct.
    /// </summary>
    private static RwTarget MapTarget(RecipeTarget target, Func<string?, IntPtr> alloc)
    {
        ArgumentNullException.ThrowIfNull(alloc);

        return target.Kind switch
        {
            RecipeTargetKind.Self => new RwTarget
            {
                Kind = RwTargetKind.Self,
                Pid = 0,
                ProcessName = IntPtr.Zero,
                LaunchPath = IntPtr.Zero,
                LaunchArguments = IntPtr.Zero,
                LaunchWorkingDirectory = IntPtr.Zero,
                LaunchStartSuspended = 0
            },

            RecipeTargetKind.ProcessById => new RwTarget
            {
                Kind = RwTargetKind.ProcessId,
                Pid = (uint)(target.ProcessId ?? throw new InvalidOperationException("RecipeTarget.ProcessId must be set when Kind is ProcessById.")),
                ProcessName = IntPtr.Zero,
                LaunchPath = IntPtr.Zero,
                LaunchArguments = IntPtr.Zero,
                LaunchWorkingDirectory = IntPtr.Zero,
                LaunchStartSuspended = 0
            },

            RecipeTargetKind.ProcessByName => new RwTarget
            {
                Kind = RwTargetKind.ProcessName,
                Pid = 0,
                ProcessName = alloc(target.ProcessName),
                LaunchPath = IntPtr.Zero,
                LaunchArguments = IntPtr.Zero,
                LaunchWorkingDirectory = IntPtr.Zero,
                LaunchStartSuspended = 0
            },

            RecipeTargetKind.LaunchProcess => new RwTarget
            {
                Kind = RwTargetKind.LaunchProcess,
                Pid = 0,
                ProcessName = IntPtr.Zero,
                LaunchPath = alloc(target.LaunchPath),
                LaunchArguments = alloc(target.LaunchArguments),
                LaunchWorkingDirectory = alloc(target.LaunchWorkingDirectory),
                LaunchStartSuspended = target.LaunchStartSuspended ? 1 : 0
            },

            _ => throw new ArgumentOutOfRangeException(nameof(target), target.Kind, "Unsupported recipe target kind."),
        };
    }

    /// <summary>
    /// Convert the native status + result struct into the managed InjectionResult.
    /// </summary>
    private static InjectionResult MapFromNativeResult(int status, RwInjectionResult nativeResult, DateTimeOffset started)
    {
        bool success = status == 0 && nativeResult.Success != 0;

        string? errorCode = nativeResult.ErrorCode != IntPtr.Zero ? Marshal.PtrToStringAnsi(nativeResult.ErrorCode) : null;

        string? errorMessage = nativeResult.ErrorMessage != IntPtr.Zero ? Marshal.PtrToStringAnsi(nativeResult.ErrorMessage) : null;

        DateTimeOffset startedAt = TryFromUnixMilliseconds(nativeResult.StartedAtUtcMs, started);
        DateTimeOffset completedAt = TryFromUnixMilliseconds(nativeResult.CompletedAtUtcMs, DateTimeOffset.UtcNow);

        return success ? InjectionResult.Succeeded(startedAt, completedAt) : InjectionResult.Failed(errorCode, errorMessage, startedAt, completedAt);
    }

    /// <summary>
    /// Convert unix milliseconds into a DateTimeOffset.
    /// If it is zero or bogus, fall back to the provided value.
    /// </summary>
    private static DateTimeOffset TryFromUnixMilliseconds(ulong value, DateTimeOffset fallback)
    {
        if (value == 0)
        {
            return fallback;
        }

        try
        {
            long signed = checked((long)value);
            return DateTimeOffset.FromUnixTimeMilliseconds(signed);
        }
        catch
        {
            return fallback;
        }
    }
}
