using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Services;

/// <summary>
/// Outcome of PipelineService.RunAsync/ExplainAsync/TraceAsync (api.md §2.1) -
/// the immediate ack, not the pipeline result. Actual stage/result data
/// arrives later as WsEvents over IEventStreamService, filtered by
/// CorrelationId.
/// </summary>
public sealed class PipelineStartResult
{
    public required bool Accepted { get; init; }

    /// <summary>Set only when Accepted is true.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Set only when Accepted is false - an ack-phase failure (malformed
    /// request, missing field, or a transport-level failure reaching the
    /// server at all). Never a pipeline error, since those arrive later as a
    /// pipeline_failed event, not here.
    /// </summary>
    public IReadOnlyList<UiError> Errors { get; init; } = [];
}
