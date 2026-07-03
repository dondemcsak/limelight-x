namespace LimelightX.UI.ViewModels.Errors;

/// <summary>
/// Domain-tagged subtype for Category:Api errors (ui-viewmodels.md §2) - also
/// used by PipelineService itself to synthesize transport-level errors (non-2xx
/// HTTP responses, malformed envelopes) that never had a wire error body.
/// </summary>
public sealed class ApiError : UiError;
