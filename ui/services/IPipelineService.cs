namespace LimelightX.UI.Services;

/// <summary>
/// Thin typed client over /src/api (ui-viewmodels.md §7.1). No caching, no
/// retries, no UI logic - deterministic request/response handling only.
/// </summary>
public interface IPipelineService
{
    Task<ExplainResult> ExplainAsync(string source);

    Task<RunResult> RunAsync(string source);

    Task<TraceResult> TraceAsync(string source);
}
