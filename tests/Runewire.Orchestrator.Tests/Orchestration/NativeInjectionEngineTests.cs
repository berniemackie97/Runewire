using System.Runtime.InteropServices;
using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Infrastructure.InjectionEngines;
using Runewire.Orchestrator.Infrastructure.NativeInterop;
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
            TechniqueParameters: new Dictionary<string, string> { { "mode", "safe" } },
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
        Assert.Contains("\"mode\":\"safe\"", fakeInvoker.TechniqueParametersJson);
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
            TechniqueParameters: null,
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

    [Fact]
    public async Task ExecuteAsync_maps_error_code_from_native_failure()
    {
        // Arrange
        CapturingFakeInvoker fakeInvoker = new()
        {
            StatusToReturn = 1,
            ResultToReturn = new RwInjectionResult
            {
                Success = 0,
                ErrorCode = Marshal.StringToHGlobalAnsi("TECHNIQUE_UNSUPPORTED"),
                ErrorMessage = Marshal.StringToHGlobalAnsi("Technique not implemented"),
                StartedAtUtcMs = 1_700_000_000_000,
                CompletedAtUtcMs = 1_700_000_000_500,
            },
        };

        NativeInjectionEngine engine = new(fakeInvoker);

        InjectionRequest request = new(
            RecipeName: "demo-recipe",
            RecipeDescription: "Demo",
            Target: RecipeTarget.Self(),
            TechniqueName: "UnknownTech",
            TechniqueParameters: null,
            PayloadPath: @"C:\lab\payloads\demo.dll",
            AllowKernelDrivers: false,
            RequireInteractiveConsent: false
        );

        // Act
        InjectionResult result = await engine.ExecuteAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("TECHNIQUE_UNSUPPORTED", result.ErrorCode);
        Assert.Equal("Technique not implemented", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_maps_launch_process_target()
    {
        // Arrange
        CapturingFakeInvoker fakeInvoker = new()
        {
            StatusToReturn = 0,
            ResultToReturn = new RwInjectionResult { Success = 1 }
        };
        NativeInjectionEngine engine = new(fakeInvoker);

        InjectionRequest request = new(
            RecipeName: "launch-demo",
            RecipeDescription: "Launch target",
            Target: RecipeTarget.ForLaunchProcess(@"C:\demo\app.exe", "--run", @"C:\work", startSuspended: true),
            TechniqueName: "CreateRemoteThread",
            TechniqueParameters: null,
            PayloadPath: @"C:\lab\payloads\demo.dll",
            AllowKernelDrivers: false,
            RequireInteractiveConsent: true
        );

        // Act
        await engine.ExecuteAsync(request);

        // Assert
        Assert.Equal(RwTargetKind.LaunchProcess, fakeInvoker.TargetKind);
        Assert.Equal(@"C:\demo\app.exe", fakeInvoker.LaunchPath);
        Assert.Equal("--run", fakeInvoker.LaunchArguments);
        Assert.Equal(@"C:\work", fakeInvoker.LaunchWorkingDirectory);
        Assert.Equal(1, fakeInvoker.LaunchStartSuspended);
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
        public string? LaunchPath { get; private set; }
        public string? LaunchArguments { get; private set; }
        public string? LaunchWorkingDirectory { get; private set; }
        public int LaunchStartSuspended { get; private set; }
        public string? TechniqueName { get; private set; }
        public string? TechniqueParametersJson { get; private set; }
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
            LaunchPath = PtrToStringOrNull(request.Target.LaunchPath);
            LaunchArguments = PtrToStringOrNull(request.Target.LaunchArguments);
            LaunchWorkingDirectory = PtrToStringOrNull(request.Target.LaunchWorkingDirectory);
            LaunchStartSuspended = request.Target.LaunchStartSuspended;

            TechniqueName = PtrToStringOrNull(request.TechniqueName);
            TechniqueParametersJson = PtrToStringOrNull(request.TechniqueParametersJson);
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

    private sealed class ThrowingFakeInvoker(Exception exception) : INativeInjectorInvoker
    {
        private readonly Exception _exception = exception;

        public int Inject(in RwInjectionRequest request, out RwInjectionResult result)
        {
            result = default;
            throw _exception;
        }
    }
}
