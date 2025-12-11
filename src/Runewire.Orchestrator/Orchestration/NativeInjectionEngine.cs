using System.Runtime.InteropServices;
using Runewire.Core.Domain.Recipes;
using Runewire.Orchestrator.NativeInterop;

namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Injection engine that delegates to the native Runewire.
/// </summary>
public sealed class NativeInjectionEngine : IInjectionEngine
{
    private readonly INativeInjectorInvoker _invoker;

    /// <summary>
    /// Creates a native injection engine that uses the default DLL invoker.
    /// </summary>
    public NativeInjectionEngine() : this(new NativeInjectorInvoker()) { }

    /// <summary>
    /// Internal constructor for tests and advanced scenarios where a custom
    /// native invoker (e.g. a fake) is required.
    /// </summary>
    internal NativeInjectionEngine(INativeInjectorInvoker invoker)
    {
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    }

    public Task<InjectionResult> ExecuteAsync(InjectionRequest request, CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset started = DateTimeOffset.UtcNow;

        List<nint> allocations = new(capacity: 6);
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

        return new RwInjectionRequest
        {
            RecipeName = Alloc(request.RecipeName),
            RecipeDescription = Alloc(request.RecipeDescription),
            Target = target,
            TechniqueName = Alloc(request.TechniqueName),
            PayloadPath = Alloc(request.PayloadPath),
            AllowKernelDrivers = request.AllowKernelDrivers ? 1 : 0,
            RequireInteractiveConsent = request.RequireInteractiveConsent ? 1 : 0,
        };
    }

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
            },

            RecipeTargetKind.ProcessById => new RwTarget
            {
                Kind = RwTargetKind.ProcessId,
                Pid = (uint)(target.ProcessId ?? throw new ArgumentNullException(nameof(target))),
                ProcessName = IntPtr.Zero,
            },

            RecipeTargetKind.ProcessByName => new RwTarget
            {
                Kind = RwTargetKind.ProcessName,
                Pid = 0,
                ProcessName = alloc(target.ProcessName),
            },

            _ => throw new ArgumentOutOfRangeException(nameof(target), target.Kind, "Unsupported recipe target kind."),
        };
    }

    private static InjectionResult MapFromNativeResult(int status, RwInjectionResult nativeResult, DateTimeOffset started)
    {
        bool success = status == 0 && nativeResult.Success != 0;

        string? errorCode = nativeResult.ErrorCode != IntPtr.Zero ? Marshal.PtrToStringAnsi(nativeResult.ErrorCode) : null;

        string? errorMessage = nativeResult.ErrorMessage != IntPtr.Zero ? Marshal.PtrToStringAnsi(nativeResult.ErrorMessage) : null;

        DateTimeOffset startedAt = TryFromUnixMilliseconds(nativeResult.StartedAtUtcMs, started);
        DateTimeOffset completedAt = TryFromUnixMilliseconds(nativeResult.CompletedAtUtcMs, DateTimeOffset.UtcNow);

        return success ? InjectionResult.Succeeded(startedAt, completedAt) : InjectionResult.Failed(errorCode, errorMessage, startedAt, completedAt);
    }

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
