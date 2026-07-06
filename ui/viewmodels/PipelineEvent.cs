namespace LimelightX.UI.ViewModels;

/// <summary>
/// Display-only record of a streamed pipeline event, backing a tab's
/// ExecutionTimeline component (ui-components.md §5.1, ui-viewmodels.md §7).
/// Purely an append-only log for rendering - it plays no part in event
/// delivery/ordering, which PipelineExecutionViewModel already handles
/// directly off the live WsEvent as it arrives.
/// </summary>
public sealed class PipelineEvent
{
    public required string EventType { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}
