using System.Text.Json;
using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Tests.TestDoubles;

/// <summary>
/// Scriptable IEventStreamService for ViewModel tests - lets a test raise
/// hand-built WsEvents in a chosen order/correlation_id without a real
/// socket (ui-testing.md §5).
/// </summary>
public sealed class FakeEventStreamService : IEventStreamService
{
    public event Action<WsEvent>? EventReceived;

    public event Action<UiError>? TransportFaulted;

    public Task ConnectAsync(int port, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Raise(WsEvent wsEvent) => EventReceived?.Invoke(wsEvent);

    public void RaiseFault(UiError error) => TransportFaulted?.Invoke(error);

    /// <summary>Builds a WsEvent with Data serialized from <paramref name="data"/> (or none, for pipeline_started/pipeline_failed).</summary>
    public static WsEvent MakeEvent(string eventType, string correlationId, object? data = null, bool success = true, IReadOnlyList<UiError>? errors = null) => new()
    {
        Version = "v1",
        Success = success,
        Errors = errors ?? [],
        Data = data is null ? null : JsonSerializer.SerializeToElement(data, PipelineJsonOptions.Default),
        EventType = eventType,
        CorrelationId = correlationId,
    };
}
