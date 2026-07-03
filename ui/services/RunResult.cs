using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Services;

/// <summary>Outcome of PipelineService.RunAsync (ui-viewmodels.md §7.1).</summary>
public sealed class RunResult
{
    public required bool Success { get; init; }

    public RunData? Data { get; init; }

    public IReadOnlyList<UiError> Errors { get; init; } = [];
}
