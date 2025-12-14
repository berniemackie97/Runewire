using Runewire.Domain.Recipes;
using Runewire.Orchestrator.Infrastructure.Preflight;

namespace Runewire.Orchestrator.Infrastructure.Services;

/// <summary>
/// Validation outcome returned by RecipeExecutionService.
/// </summary>
public sealed record RecipeValidationOutcome(RunewireRecipe Recipe, TargetPreflightResult TargetPreflight, PayloadPreflightResult PayloadPreflight, PreflightSummary Preflight);
