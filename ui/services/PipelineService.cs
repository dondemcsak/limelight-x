using System.Net.Http.Json;
using System.Text.Json;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Services;

/// <summary>
/// Concrete IPipelineService wrapping /src/api's POST /run, /explain, /trace
/// (spec/api.md, ui-data-contracts.md). Owns a single long-lived HttpClient -
/// no DI container is on the approved dependency list (CLAUDE.md §3.5), so
/// this is constructed once in the composition root (App.axaml.cs) and its
/// port is repointed in place via SetPort when SettingsViewModel relaunches
/// llx serve on a new port, rather than replacing the instance.
///
/// Each call only returns the immediate ack (or an ack-phase failure) - the
/// actual pipeline result streams later over IEventStreamService, filtered
/// by the returned CorrelationId.
/// </summary>
public sealed class PipelineService : IPipelineService, IDisposable
{
    private readonly HttpClient _httpClient;

    public PipelineService(int port)
    {
        _httpClient = new HttpClient();
        SetPort(port);
    }

    /// <summary>Repoints this client at a new port after SettingsViewModel relaunches llx serve.</summary>
    public void SetPort(int port)
    {
        _httpClient.BaseAddress = new Uri($"http://127.0.0.1:{port}");
    }

    public Task<PipelineStartResult> ExplainAsync(string source) => PostAsync("/explain", source);

    public Task<PipelineStartResult> RunAsync(string source) => PostAsync("/run", source);

    public Task<PipelineStartResult> TraceAsync(string source) => PostAsync("/trace", source);

    private async Task<PipelineStartResult> PostAsync(string endpoint, string source)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .PostAsJsonAsync(endpoint, new { source }, PipelineJsonOptions.Default)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // The server never responded at all (not running, wrong port, etc.) -
            // there is no wire envelope to parse, so this is synthesized entirely
            // client-side as a Transport error (ui-error-handling.md §4), not an
            // ack-phase api.md §10 error, since the server never saw the request.
            return new PipelineStartResult
            {
                Accepted = false,
                Errors = [TransportError($"Could not reach the Limelight-X server: {ex.Message}")],
            };
        }

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            AckResponse? ack;
            try
            {
                ack = JsonSerializer.Deserialize<AckResponse>(body, PipelineJsonOptions.Default);
            }
            catch (JsonException)
            {
                ack = null;
            }

            if (ack is { Accepted: true })
            {
                return new PipelineStartResult { Accepted = true, CorrelationId = ack.CorrelationId };
            }

            return new PipelineStartResult
            {
                Accepted = false,
                Errors = [TransportError($"Malformed acknowledgment from server (HTTP {(int)response.StatusCode}).")],
            };
        }

        // Ack-phase failure (api.md §10): a plain error envelope, no correlation_id.
        Envelope<object>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<Envelope<object>>(body, PipelineJsonOptions.Default);
        }
        catch (JsonException)
        {
            envelope = null;
        }

        if (envelope is null)
        {
            return new PipelineStartResult
            {
                Accepted = false,
                Errors = [TransportError($"Malformed response from server (HTTP {(int)response.StatusCode}).")],
            };
        }

        return new PipelineStartResult { Accepted = false, Errors = envelope.Errors };
    }

    private static TransportError TransportError(string message) => new()
    {
        Code = "ERR_TRANSPORT",
        Message = message,
        Severity = ErrorSeverity.Fatal,
        Category = ErrorCategory.Transport,
    };

    public void Dispose() => _httpClient.Dispose();
}
