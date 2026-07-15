using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Services;

/// <summary>
/// WebSocket client for /src/api's /events stream (api.md §2.3). Owns a
/// single persistent connection - exactly one UI client is expected. The
/// composition root must ConnectAsync before any pipeline execution can be
/// triggered (both at startup and after a Settings-triggered relaunch), so
/// no event is ever dropped for lack of a connected client.
/// </summary>
public interface IEventStreamService
{
    Task ConnectAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>Raised for every event frame received, regardless of which correlation_id it belongs to - subscribers filter for themselves.</summary>
    event Action<WsEvent>? EventReceived;

    /// <summary>Raised when the connection is lost or a frame can't be parsed - never raised by the server itself (ui-error-handling.md §4).</summary>
    event Action<UiError>? TransportFaulted;
}
