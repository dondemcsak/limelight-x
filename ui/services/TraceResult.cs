using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Services;

/// <summary>Outcome of PipelineService.TraceAsync (ui-viewmodels.md §7.1).</summary>
public sealed class TraceResult
{
    public required bool Success { get; init; }

    public TraceData? Data { get; init; }

    public IReadOnlyList<UiError> Errors { get; init; } = [];
}
