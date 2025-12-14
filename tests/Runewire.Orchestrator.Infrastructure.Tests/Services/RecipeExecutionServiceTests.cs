using Runewire.Core.Infrastructure.Recipes;
using Runewire.Domain.Recipes;
using Runewire.Domain.Validation;
using Runewire.Orchestrator.Infrastructure.InjectionEngines;
using Runewire.Orchestrator.Infrastructure.Preflight;
using Runewire.Orchestrator.Infrastructure.Services;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.Tests.Services;

public sealed class RecipeExecutionServiceTests
{
    [Fact]
    public void Validate_throws_when_preflight_fails()
    {
        // Arrange
        FakeLoaderProvider loaderProvider = new(new RunewireRecipe(
            "bad",
            null,
            RecipeTarget.ForProcessId(123),
            new InjectionTechnique("CreateRemoteThread"),
            @"C:\payloads\demo.dll",
            RequireInteractiveConsent: false,
            AllowKernelDrivers: false));

        FakePreflightChecker preflight = new(TargetPreflightResult.Failed(new RecipeValidationError("TARGET_PID_NOT_FOUND", "missing")));
        RecipeExecutionService service = new(loaderProvider, preflight, new FakeEngineFactory());

        // Act/Assert
        RecipeLoadException ex = Assert.Throws<RecipeLoadException>(() => service.Validate("demo.yaml"));
        Assert.Contains(ex.ValidationErrors, e => e.Code == "TARGET_PID_NOT_FOUND");
    }

    [Fact]
    public async Task RunAsync_executes_when_validation_passes()
    {
        // Arrange
        RunewireRecipe recipe = new(
            "ok",
            null,
            RecipeTarget.Self(),
            new InjectionTechnique("CreateRemoteThread"),
            @"C:\payloads\demo.dll",
            RequireInteractiveConsent: false,
            AllowKernelDrivers: false);

        FakeLoaderProvider loaderProvider = new(recipe);
        RecipeExecutionService service = new(loaderProvider, new NullTargetPreflightChecker(), new FakeEngineFactory());

        // Act
        RecipeRunOutcome outcome = await service.RunAsync("demo.yaml", useNativeEngine: false);

        // Assert
        Assert.True(outcome.InjectionResult.Success);
        Assert.Equal(recipe, outcome.Recipe);
    }

    private sealed class FakeLoaderProvider(RunewireRecipe recipe) : IRecipeLoaderProvider
    {
        public IRecipeLoader Create(string path) => new FakeLoader(recipe);

        private sealed class FakeLoader(RunewireRecipe recipe) : IRecipeLoader
        {
            public RunewireRecipe LoadFromFile(string path) => recipe;
            public RunewireRecipe LoadFromString(string text) => recipe;
        }
    }

    private sealed class FakePreflightChecker(TargetPreflightResult result) : ITargetPreflightChecker
    {
        public TargetPreflightResult Check(RunewireRecipe recipe) => result;
    }

    private sealed class FakeEngineFactory : IInjectionEngineFactory
    {
        public IInjectionEngine Create(bool useNativeEngine) => new FakeEngine();

        private sealed class FakeEngine : IInjectionEngine
        {
            public Task<InjectionResult> ExecuteAsync(InjectionRequest request, CancellationToken cancellationToken = default)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                return Task.FromResult(InjectionResult.Succeeded(now, now));
            }
        }
    }
}
