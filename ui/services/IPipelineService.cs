namespace LimelightX.UI.Services;

/// <summary>
/// Thin typed client over /src/api's POST /run|/explain|/trace
/// (ui-viewmodels.md §5). Each call only returns the immediate ack or an
/// ack-phase failure - actual pipeline results arrive later as events over
/// IEventStreamService. No caching, no retries.
/// </summary>
public interface IPipelineService
{
    Task<PipelineStartResult> ExplainAsync(string source);

    Task<PipelineStartResult> RunAsync(string source);

    Task<PipelineStartResult> TraceAsync(string source);
}
