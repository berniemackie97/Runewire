using Runewire.Domain.Recipes;

namespace Runewire.Orchestrator.Orchestration;

/// <summary>
/// Executes a validated recipe by mapping it into an InjectionRequest (or step sequence) and sending it to the engine.
/// </summary>
public sealed class RecipeExecutor(IInjectionEngine injectionEngine, ITargetController targetController, ITargetObserver targetObserver)
{
    private readonly IInjectionEngine _injectionEngine = injectionEngine ?? throw new ArgumentNullException(nameof(injectionEngine));
    private readonly ITargetController _targetController = targetController ?? throw new ArgumentNullException(nameof(targetController));
    private readonly ITargetObserver _targetObserver = targetObserver ?? throw new ArgumentNullException(nameof(targetObserver));

    /// <summary>
    /// Execute a recipe using the configured engine and optional workflow steps.
    /// </summary>
    public async Task<RecipeExecutionResult> ExecuteAsync(RunewireRecipe recipe, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        cancellationToken.ThrowIfCancellationRequested();

        if (recipe.Steps is null || recipe.Steps.Count == 0)
        {
            InjectionRequest request = MapToRequest(recipe, recipe.Technique.Name, recipe.PayloadPath, recipe.Technique.Parameters);
            InjectionResult result = await _injectionEngine.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            RecipeStepResult stepResult = RecipeStepResult.FromInjection(0, RecipeStep.Inject(recipe.Technique.Name, recipe.PayloadPath, recipe.Technique.Parameters), result);
            return new RecipeExecutionResult(result, new[] { stepResult });
        }

        List<RecipeStepResult> stepResults = [];
        DateTimeOffset overallStarted = DateTimeOffset.UtcNow;
        DateTimeOffset overallCompleted = overallStarted;

        int index = 0;
        foreach (RecipeStep step in recipe.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset stepStarted = DateTimeOffset.UtcNow;

            switch (step.Kind)
            {
                case RecipeStepKind.InjectTechnique:
                    InjectionRequest stepRequest = MapToRequest(recipe, step.TechniqueName!, step.PayloadPath!, step.TechniqueParameters);
                    InjectionResult injectionResult = await _injectionEngine.ExecuteAsync(stepRequest, cancellationToken).ConfigureAwait(false);
                    overallCompleted = injectionResult.CompletedAtUtc;
                    RecipeStepResult injectStepResult = RecipeStepResult.FromInjection(index, step, injectionResult);
                    stepResults.Add(injectStepResult);
                    if (!injectionResult.Success)
                    {
                        return new RecipeExecutionResult(BuildOverallResult(overallStarted, overallCompleted, injectStepResult), stepResults);
                    }
                    break;

                case RecipeStepKind.Wait:
                    RecipeStepResult waitResult = await ExecuteWaitStepAsync(index, step, recipe.Target, stepStarted, cancellationToken).ConfigureAwait(false);
                    overallCompleted = waitResult.CompletedAtUtc;
                    stepResults.Add(waitResult);
                    if (!waitResult.Success)
                    {
                        return new RecipeExecutionResult(BuildOverallResult(overallStarted, overallCompleted, waitResult), stepResults);
                    }
                    break;

                case RecipeStepKind.Suspend:
                case RecipeStepKind.Resume:
                    TargetControlResult controlResult = step.Kind == RecipeStepKind.Suspend
                        ? await _targetController.SuspendAsync(recipe.Target, cancellationToken).ConfigureAwait(false)
                        : await _targetController.ResumeAsync(recipe.Target, cancellationToken).ConfigureAwait(false);

                    overallCompleted = DateTimeOffset.UtcNow;
                    RecipeStepResult controlStepResult = RecipeStepResult.FromControl(index, step, controlResult, stepStarted, overallCompleted);
                    stepResults.Add(controlStepResult);
                    if (!controlResult.Success)
                    {
                        return new RecipeExecutionResult(BuildOverallResult(overallStarted, overallCompleted, controlStepResult), stepResults);
                    }
                    break;

                default:
                    RecipeStepResult unknown = new(index, step.Kind, false, "STEP_KIND_UNKNOWN", $"Unsupported step kind '{step.Kind}'.", stepStarted, DateTimeOffset.UtcNow, null);
                    stepResults.Add(unknown);
                    return new RecipeExecutionResult(BuildOverallResult(overallStarted, unknown.CompletedAtUtc, unknown), stepResults);
            }

            index++;
        }

        InjectionResult overallSuccess = InjectionResult.Succeeded(overallStarted, overallCompleted);
        return new RecipeExecutionResult(overallSuccess, stepResults);
    }

    /// <summary>
    /// Convert the domain recipe into the engine request shape.
    /// </summary>
    private static InjectionRequest MapToRequest(RunewireRecipe recipe, string techniqueName, string payloadPath, IReadOnlyDictionary<string, string>? parameters)
    {
        return new InjectionRequest(
            recipe.Name,
            recipe.Description,
            recipe.Target,
            techniqueName,
            parameters,
            payloadPath,
            recipe.AllowKernelDrivers,
            recipe.RequireInteractiveConsent
        );
    }

    private static InjectionResult BuildOverallResult(DateTimeOffset startedAt, DateTimeOffset completedAt, RecipeStepResult failedStep)
    {
        if (failedStep.Success)
        {
            return InjectionResult.Succeeded(startedAt, completedAt);
        }

        string errorCode = string.IsNullOrWhiteSpace(failedStep.ErrorCode) ? "STEP_FAILED" : failedStep.ErrorCode!;
        string? errorMessage = string.IsNullOrWhiteSpace(failedStep.ErrorMessage)
            ? $"Step {failedStep.Index} failed."
            : failedStep.ErrorMessage;

        return InjectionResult.Failed(errorCode, errorMessage, startedAt, completedAt);
    }

    private async Task<RecipeStepResult> ExecuteWaitStepAsync(int index, RecipeStep step, RecipeTarget target, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        if (step.Condition is null)
        {
            int delayMs = step.WaitMilliseconds ?? 0;
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            return RecipeStepResult.Wait(index, step, startedAt, completed);
        }

        WaitResult wait = await _targetObserver.WaitForAsync(target, step.Condition, cancellationToken).ConfigureAwait(false);
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        if (wait.Success)
        {
            return RecipeStepResult.Wait(index, step, startedAt, completedAt);
        }

        string code = string.IsNullOrWhiteSpace(wait.ErrorCode) ? "WAIT_FAILED" : wait.ErrorCode!;
        string message = string.IsNullOrWhiteSpace(wait.ErrorMessage) ? "Wait condition failed." : wait.ErrorMessage!;
        return new RecipeStepResult(index, step.Kind, false, code, message, startedAt, completedAt, null);
    }
}
