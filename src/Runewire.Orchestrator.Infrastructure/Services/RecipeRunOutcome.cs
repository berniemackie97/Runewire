using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Infrastructure.Preflight;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Orchestrator.Infrastructure.Services;

/// <summary>
/// Run outcome returned by RecipeExecutionService.
/// </summary>
public sealed record RecipeRunOutcome(RunewireRecipe Recipe, InjectionResult InjectionResult, string Engine, PreflightSummary Preflight);
