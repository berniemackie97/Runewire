using Runewire.Domain.Validation;
using Runewire.Domain.Techniques;
using Runewire.Orchestrator.Infrastructure.Preflight;
using Runewire.Orchestrator.Infrastructure.Services;
using Runewire.Orchestrator.Orchestration;

namespace Runewire.Cli.Infrastructure.Output;

public sealed record MetaDto(string? Version);
public sealed record ErrorDto(string Code, string Message);
public sealed record PreflightSummaryDto(
    bool TargetSuccess,
    IReadOnlyList<ErrorDto> TargetErrors,
    bool PayloadSuccess,
    IReadOnlyList<ErrorDto> PayloadErrors,
    string? PayloadArchitecture,
    string? ProcessArchitecture
);
public sealed record InjectionResultDto(bool Success, string? ErrorCode, string? ErrorMessage, DateTimeOffset StartedAtUtc, DateTimeOffset CompletedAtUtc);
public sealed record TechniqueParameterDto(string Name, string Description, bool Required, string DataType);
public sealed record TechniqueDto(string Name, string DisplayName, string Category, string Description, bool RequiresKernelMode, bool RequiresDriver, string? MinNativeVersion, bool Implemented, IReadOnlyList<string> Platforms, IReadOnlyList<TechniqueParameterDto> Parameters);

public sealed record ValidationResponseDto(
    string Status,
    string? RecipeName,
    MetaDto Meta,
    PreflightSummaryDto? Preflight,
    IReadOnlyList<ErrorDto>? Errors,
    string? Message,
    string? Inner
);

public sealed record RunResponseDto(
    string Status,
    string RecipeName,
    string Engine,
    MetaDto Meta,
    PreflightSummaryDto? Preflight,
    InjectionResultDto? Result,
    IReadOnlyList<ErrorDto>? Errors,
    string? Message,
    string? Inner
);

public sealed record TechniqueListResponseDto(string Status, MetaDto Meta, IReadOnlyList<TechniqueDto> Techniques);

public static class JsonResponseFactory
{
    public static ValidationResponseDto ValidationSuccess(RecipeValidationOutcome outcome) =>
        new("valid", outcome.Recipe.Name, BuildMeta(), MapPreflight(outcome.Preflight), null, null, null);

    public static ValidationResponseDto ValidationInvalid(IEnumerable<RecipeValidationError> errors) => new("invalid", null, BuildMeta(), null, MapErrors(errors), null, null);

    public static ValidationResponseDto ValidationError(string message, Exception? ex) => new("error", null, BuildMeta(), null, null, message, ex?.Message);

    public static RunResponseDto RunSuccess(RecipeRunOutcome outcome) =>
        new("succeeded", outcome.Recipe.Name, outcome.Engine, BuildMeta(), MapPreflight(outcome.Preflight), MapInjectionResult(outcome.InjectionResult), null, null, null);

    public static RunResponseDto RunFailure(RecipeRunOutcome outcome) =>
        new("failed", outcome.Recipe.Name, outcome.Engine, BuildMeta(), MapPreflight(outcome.Preflight), MapInjectionResult(outcome.InjectionResult), null, null, null);

    public static RunResponseDto RunError(string recipeName, string engine, string message, Exception? ex = null) =>
        new("error", recipeName, engine, BuildMeta(), null, null, null, message, ex?.Message);

    public static TechniqueListResponseDto TechniqueList(IEnumerable<InjectionTechniqueDescriptor> techniques) =>
        new("ok", BuildMeta(), techniques.Select(MapTechnique).ToArray());

    private static MetaDto BuildMeta()
    {
        Version? version = typeof(Program).Assembly.GetName().Version;
        return new MetaDto(version?.ToString() ?? "unknown");
    }

    private static PreflightSummaryDto MapPreflight(PreflightSummary summary)
    {
        return new PreflightSummaryDto(
            summary.TargetSuccess,
            MapErrors(summary.TargetErrors),
            summary.PayloadSuccess,
            MapErrors(summary.PayloadErrors),
            summary.PayloadArchitecture,
            summary.ProcessArchitecture);
    }

    private static InjectionResultDto MapInjectionResult(InjectionResult result) =>
        new(result.Success, result.ErrorCode, result.ErrorMessage, result.StartedAtUtc, result.CompletedAtUtc);

    private static ErrorDto[] MapErrors(IEnumerable<RecipeValidationError> errors) => [.. errors.Select(e => new ErrorDto(e.Code, e.Message))];

    private static TechniqueDto MapTechnique(InjectionTechniqueDescriptor descriptor)
    {
        IReadOnlyList<string> platforms = descriptor.Platforms.Select(p => p.ToString()).ToArray();
        IReadOnlyList<TechniqueParameterDto> parameters = descriptor.Parameters.Select(MapParameter).ToArray();
        return new TechniqueDto(descriptor.Name, descriptor.DisplayName, descriptor.Category, descriptor.Description, descriptor.RequiresKernelMode, descriptor.RequiresDriver, descriptor.MinNativeVersion, descriptor.Implemented, platforms, parameters);
    }

    private static TechniqueParameterDto MapParameter(TechniqueParameter parameter) =>
        new(parameter.Name, parameter.Description, parameter.Required, parameter.DataType);
}
