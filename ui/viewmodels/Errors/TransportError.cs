namespace LimelightX.UI.ViewModels.Errors;

/// <summary>
/// Domain-tagged subtype for Category:Transport errors (ui-error-handling.md
/// §4) - client-synthesized for failures below the pipeline: the server
/// couldn't be reached at all, the WebSocket disconnected, or an event frame
/// was malformed. Never sent by the server itself (ui-data-contracts.md §3).
/// </summary>
public sealed class TransportError : UiError;
