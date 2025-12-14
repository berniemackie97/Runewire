using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Tests.Orchestration;

public class RecipeExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_maps_recipe_to_injection_request_and_calls_engine()
    {
        // Setup
        FakeInjectionEngine fakeEngine = new();
        FakeTargetController targetController = new();
        FakeTargetObserver targetObserver = new();
        RecipeExecutor executor = new(fakeEngine, targetController, targetObserver);

        RunewireRecipe recipe = new(
            Name: "demo-recipe",
            Description: "Demo execution test.",
            Target: RecipeTarget.ForProcessName("explorer.exe"),
            Technique: new InjectionTechnique("CreateRemoteThread"),
            PayloadPath: @"C:\lab\payloads\demo.dll",
            RequireInteractiveConsent: true,
            AllowKernelDrivers: false
        );

        // Run
        RecipeExecutionResult result = await executor.ExecuteAsync(recipe);

        // Assert
        Assert.True(fakeEngine.WasCalled);
        Assert.NotNull(fakeEngine.LastRequest);

        InjectionRequest request = fakeEngine.LastRequest!;

        Assert.Equal("demo-recipe", request.RecipeName);
        Assert.Equal("Demo execution test.", request.RecipeDescription);
        Assert.Equal(recipe.Target, request.Target);
        Assert.Equal("CreateRemoteThread", request.TechniqueName);
        Assert.Equal(@"C:\lab\payloads\demo.dll", request.PayloadPath);
        Assert.True(request.RequireInteractiveConsent);
        Assert.False(request.AllowKernelDrivers);

        Assert.True(result.OverallResult.Success);
        Assert.Single(result.StepResults);
        Assert.True(result.StepResults[0].Success);
    }

    [Fact]
    public async Task ExecuteAsync_throws_on_null_recipe()
    {
        // Setup
        FakeInjectionEngine fakeEngine = new();
        RecipeExecutor executor = new(fakeEngine, new FakeTargetController(), new FakeTargetObserver());

        // Run & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => executor.ExecuteAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_runs_steps_and_stops_on_failure()
    {
        // Setup
        FakeInjectionEngine fakeEngine = new(failOnSecond: true);
        FakeTargetController targetController = new();
        FakeTargetObserver targetObserver = new();
        RecipeExecutor executor = new(fakeEngine, targetController, targetObserver);

        RunewireRecipe recipe = new(
            Name: "workflow",
            Description: "Workflow execution.",
            Target: RecipeTarget.ForProcessId(123),
            Technique: new InjectionTechnique("BaseTechnique"),
            PayloadPath: @"C:\lab\payloads\base.dll",
            RequireInteractiveConsent: true,
            AllowKernelDrivers: false,
            Steps: new List<RecipeStep>
            {
                RecipeStep.Suspend(),
                RecipeStep.Inject("StepTech1", @"C:\lab\payloads\one.dll"),
                RecipeStep.WaitFor(new WaitCondition(WaitConditionKind.FileExists, @"C:\temp\flag", 10)),
                RecipeStep.Inject("StepTech2", @"C:\lab\payloads\two.dll"),
                RecipeStep.Resume(),
            });

        // Run
        RecipeExecutionResult result = await executor.ExecuteAsync(recipe);

        // Assert
        Assert.False(result.OverallResult.Success);
        Assert.Equal(2, fakeEngine.CallCount); // two injection attempts, stop after failure
        Assert.True(targetController.SuspendCalled);
        Assert.False(targetController.ResumeCalled); // stopped before resume step
        Assert.True(targetObserver.WaitCalled);

        RecipeStepResult failedStep = Assert.Single(result.StepResults, s => !s.Success);
        Assert.Equal("INJECTION_FAILED", failedStep.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_fails_when_wait_condition_fails()
    {
        FakeInjectionEngine fakeEngine = new();
        FakeTargetController targetController = new();
        FakeTargetObserver targetObserver = new(fail: true);
        RecipeExecutor executor = new(fakeEngine, targetController, targetObserver);

        RunewireRecipe recipe = new(
            Name: "workflow",
            Description: "Workflow execution.",
            Target: RecipeTarget.ForProcessId(123),
            Technique: new InjectionTechnique("BaseTechnique"),
            PayloadPath: @"C:\lab\payloads\base.dll",
            RequireInteractiveConsent: true,
            AllowKernelDrivers: false,
            Steps: new List<RecipeStep>
            {
                RecipeStep.WaitFor(new WaitCondition(WaitConditionKind.FileExists, @"C:\temp\flag", 50)),
                RecipeStep.Inject("StepTech", @"C:\lab\payloads\one.dll")
            });

        RecipeExecutionResult result = await executor.ExecuteAsync(recipe);

        Assert.False(result.OverallResult.Success);
        Assert.Equal("WAIT_CONDITION_FAILED", result.OverallResult.ErrorCode);
        Assert.False(targetObserver.LastResultSuccess);
    }

    private sealed class FakeInjectionEngine : IInjectionEngine
    {
        public bool WasCalled { get; private set; }
        public InjectionRequest? LastRequest { get; private set; }
        public int CallCount { get; private set; }
        private readonly bool _failOnSecond;

        public FakeInjectionEngine(bool failOnSecond = false)
        {
            _failOnSecond = failOnSecond;
        }

        public Task<InjectionResult> ExecuteAsync(InjectionRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastRequest = request;
            CallCount++;

            DateTimeOffset now = DateTimeOffset.UtcNow;

            if (_failOnSecond && CallCount >= 2)
            {
                return Task.FromResult(InjectionResult.Failed("INJECTION_FAILED", "injection failed", now, now));
            }

            return Task.FromResult(InjectionResult.Succeeded(now, now));
        }
    }

    private sealed class FakeTargetController : ITargetController
    {
        public bool SuspendCalled { get; private set; }
        public bool ResumeCalled { get; private set; }

        public Task<TargetControlResult> ResumeAsync(RecipeTarget target, CancellationToken cancellationToken = default)
        {
            ResumeCalled = true;
            return Task.FromResult(TargetControlResult.Succeeded());
        }

        public Task<TargetControlResult> SuspendAsync(RecipeTarget target, CancellationToken cancellationToken = default)
        {
            SuspendCalled = true;
            return Task.FromResult(TargetControlResult.Succeeded());
        }
    }

    private sealed class FakeTargetObserver : ITargetObserver
    {
        public bool WaitCalled { get; private set; }
        public bool LastResultSuccess { get; private set; } = true;
        private readonly bool _fail;

        public FakeTargetObserver(bool fail = false)
        {
            _fail = fail;
        }

        public Task<WaitResult> WaitForAsync(RecipeTarget target, WaitCondition condition, CancellationToken cancellationToken = default)
        {
            WaitCalled = true;
            LastResultSuccess = !_fail;
            return Task.FromResult(_fail ? WaitResult.Failed("WAIT_CONDITION_FAILED", "failed") : WaitResult.Succeeded());
        }
    }
}
