using System.Text.Json;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Services.Dto;

/// <summary>
/// A single streamed WebSocket event (ui-data-contracts.md §1, api.md §2.1) -
/// the same envelope shape as the old single-response body plus
/// EventType/CorrelationId. Data's shape depends on EventType, so it's left
/// as a raw JsonElement here and deserialized into the matching *EventData
/// type by whichever ViewModel is dispatching on EventType.
/// </summary>
public sealed class WsEvent
{
    public required string Version { get; init; }

    public required bool Success { get; init; }

    public IReadOnlyList<UiError> Errors { get; init; } = [];

    public JsonElement? Data { get; init; }

    public required string EventType { get; init; }

    public required string CorrelationId { get; init; }
}
