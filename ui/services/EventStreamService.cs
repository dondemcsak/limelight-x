using System.Net.WebSockets;
using System.Text.Json;
using Avalonia.Threading;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Services;

/// <summary>
/// Concrete IEventStreamService wrapping /src/api's ws://127.0.0.1:&lt;port&gt;/events
/// (api.md §2.3). Constructed once in the composition root (App.axaml.cs);
/// ConnectAsync tears down any previous connection first, which is also how
/// a Settings-triggered `llx serve` relaunch is handled (reconnect to the
/// new port rather than replacing the instance, mirroring PipelineService's
/// SetPort pattern).
/// </summary>
public sealed class EventStreamService : IEventStreamService, IDisposable
{
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _receiveLoopCts;

    public event Action<WsEvent>? EventReceived;

    public event Action<UiError>? TransportFaulted;

    public async Task ConnectAsync(int port, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync().ConfigureAwait(false);

        var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/events"), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or TaskCanceledException)
        {
            socket.Dispose();
            RaiseTransportFaulted(MakeTransportError($"Could not connect to the event stream: {ex.Message}"));
            throw;
        }

        _socket = socket;
        var cts = new CancellationTokenSource();
        _receiveLoopCts = cts;
        _ = ReceiveLoopAsync(socket, cts.Token);
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var stream = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    stream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                stream.Position = 0;
                var wsEvent = await JsonSerializer
                    .DeserializeAsync<WsEvent>(stream, PipelineJsonOptions.Default, cancellationToken)
                    .ConfigureAwait(false);
                if (wsEvent is not null)
                {
                    RaiseEventReceived(wsEvent);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on disconnect/shutdown - not a fault.
        }
        catch (Exception ex) when (ex is WebSocketException or JsonException)
        {
            RaiseTransportFaulted(MakeTransportError($"Event stream connection lost: {ex.Message}"));
        }
    }

    /// <summary>
    /// Every await above is ConfigureAwait(false), so this loop runs on a
    /// thread-pool thread, not the Avalonia UI thread. Subscribers
    /// (PipelineExecutionViewModel, EditorViewModel) mutate UI-bound
    /// ObservableCollections/properties in their handlers, which throws if
    /// touched off the UI thread - so events must be marshaled through the
    /// dispatcher here, once, rather than trusting every subscriber to do it.
    /// </summary>
    private void RaiseEventReceived(WsEvent wsEvent) => Dispatcher.UIThread.Post(() => EventReceived?.Invoke(wsEvent));

    private void RaiseTransportFaulted(UiError error) => Dispatcher.UIThread.Post(() => TransportFaulted?.Invoke(error));

    public async Task DisconnectAsync()
    {
        _receiveLoopCts?.Cancel();
        _receiveLoopCts = null;

        var socket = _socket;
        _socket = null;
        if (socket is null)
        {
            return;
        }

        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
            // Already closed/faulted on the other end - fine to drop.
        }
        finally
        {
            socket.Dispose();
        }
    }

    private static TransportError MakeTransportError(string message) => new()
    {
        Code = "ERR_TRANSPORT",
        Message = message,
        Severity = ErrorSeverity.Fatal,
        Category = ErrorCategory.Transport,
    };

    public void Dispose() => _ = DisconnectAsync();
}
