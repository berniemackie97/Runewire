using System.Runtime.InteropServices;
using Runewire.Core.Domain.Recipes;
using Runewire.Orchestrator.NativeInterop;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Tests.Orchestration;

public class NativeInjectionEngineTests
{
    [Fact]
    public async Task ExecuteAsync_maps_request_and_success_result_correctly()
    {
        // Arrange
        CapturingFakeInvoker fakeInvoker = new()
        {
            StatusToReturn = 0,
            ResultToReturn = new RwInjectionResult
            {
                Success = 1,
                ErrorCode = IntPtr.Zero,
                ErrorMessage = IntPtr.Zero,
                StartedAtUtcMs = 1_700_000_000_000, // arbitrary stable values
                CompletedAtUtcMs = 1_700_000_000_500,
            },
        };

        NativeInjectionEngine engine = new(fakeInvoker);

        RecipeTarget recipeTarget = RecipeTarget.ForProcessName("explorer.exe");

        InjectionRequest request = new(
            RecipeName: "demo-recipe",
            RecipeDescription: "Demo via native engine",
            Target: recipeTarget,
            TechniqueName: "CreateRemoteThread",
            PayloadPath: @"C:\lab\payloads\demo.dll",
            AllowKernelDrivers: false,
            RequireInteractiveConsent: true
        );

        // Act
        InjectionResult result = await engine.ExecuteAsync(request);

        // Assert: mapping to native request
        Assert.Equal("demo-recipe", fakeInvoker.RecipeName);
        Assert.Equal("Demo via native engine", fakeInvoker.RecipeDescription);
        Assert.Equal(RwTargetKind.ProcessName, fakeInvoker.TargetKind);
        Assert.Equal("explorer.exe", fakeInvoker.TargetProcessName);
        Assert.Equal("CreateRemoteThread", fakeInvoker.TechniqueName);
        Assert.Equal(@"C:\lab\payloads\demo.dll", fakeInvoker.PayloadPath);
        Assert.Equal(0, fakeInvoker.AllowKernelDrivers);
        Assert.Equal(1, fakeInvoker.RequireInteractiveConsent);

        // Assert: mapping back from native result
        Assert.True(result.Success);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);

        DateTimeOffset expectedStarted = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000);
        DateTimeOffset expectedCompleted = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_500);

        Assert.Equal(expectedStarted, result.StartedAtUtc);
        Assert.Equal(expectedCompleted, result.CompletedAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_returns_failed_result_when_invoker_throws()
    {
        // Arrange
        ThrowingFakeInvoker failingInvoker = new(
            new DllNotFoundException("Runewire.Injector.dll")
        );
        NativeInjectionEngine engine = new(failingInvoker);

        InjectionRequest request = new(
            RecipeName: "demo-recipe",
            RecipeDescription: null,
            Target: RecipeTarget.Self(),
            TechniqueName: "CreateRemoteThread",
            PayloadPath: @"C:\lab\payloads\demo.dll",
            AllowKernelDrivers: false,
            RequireInteractiveConsent: false
        );

        // Act
        InjectionResult result = await engine.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("NATIVE_INVOKE_FAILED", result.ErrorCode);
        Assert.Contains("Runewire.Injector.dll", result.ErrorMessage);
    }

    private sealed class CapturingFakeInvoker : INativeInjectorInvoker
    {
        public int StatusToReturn { get; set; }
        public RwInjectionResult ResultToReturn { get; set; }

        // Captured fields for assertions
        public string? RecipeName { get; private set; }
        public string? RecipeDescription { get; private set; }
        public RwTargetKind TargetKind { get; private set; }
        public uint TargetPid { get; private set; }
        public string? TargetProcessName { get; private set; }
        public string? TechniqueName { get; private set; }
        public string? PayloadPath { get; private set; }
        public int AllowKernelDrivers { get; private set; }
        public int RequireInteractiveConsent { get; private set; }

        public int Inject(in RwInjectionRequest request, out RwInjectionResult result)
        {
            RecipeName = PtrToStringOrNull(request.RecipeName);
            RecipeDescription = PtrToStringOrNull(request.RecipeDescription);

            TargetKind = request.Target.Kind;
            TargetPid = request.Target.Pid;
            TargetProcessName = PtrToStringOrNull(request.Target.ProcessName);

            TechniqueName = PtrToStringOrNull(request.TechniqueName);
            PayloadPath = PtrToStringOrNull(request.PayloadPath);

            AllowKernelDrivers = request.AllowKernelDrivers;
            RequireInteractiveConsent = request.RequireInteractiveConsent;

            result = ResultToReturn;
            return StatusToReturn;
        }

        private static string? PtrToStringOrNull(IntPtr ptr)
        {
            return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
        }
    }

    private sealed class ThrowingFakeInvoker : INativeInjectorInvoker
    {
        private readonly Exception _exception;

        public ThrowingFakeInvoker(Exception exception)
        {
            _exception = exception;
        }

        public int Inject(in RwInjectionRequest request, out RwInjectionResult result)
        {
            result = default;
            throw _exception;
        }
    }
}
