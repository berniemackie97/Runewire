using Runewire.Core.Domain.Recipes;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Tests.Orchestration;

public class RecipeExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_maps_recipe_to_injection_request_and_calls_engine()
    {
        // Setup
        FakeInjectionEngine fakeEngine = new();
        RecipeExecutor executor = new(fakeEngine);

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
        InjectionResult result = await executor.ExecuteAsync(recipe);

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

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_throws_on_null_recipe()
    {
        // Setup
        FakeInjectionEngine fakeEngine = new();
        RecipeExecutor executor = new(fakeEngine);

        // Run & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => executor.ExecuteAsync(null!));
    }

    private sealed class FakeInjectionEngine : IInjectionEngine
    {
        public bool WasCalled { get; private set; }
        public InjectionRequest? LastRequest { get; private set; }

        public Task<InjectionResult> ExecuteAsync(InjectionRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastRequest = request;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            return Task.FromResult(InjectionResult.Succeeded(now, now));
        }
    }
}
