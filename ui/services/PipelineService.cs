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
/// llx serve on a new port (Phase 6), rather than replacing the instance.
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

    public async Task<ExplainResult> ExplainAsync(string source)
    {
        var (success, data, errors) = await PostAsync<ExplainData>("/explain", source).ConfigureAwait(false);
        return new ExplainResult { Success = success, Data = data, Errors = errors };
    }

    public async Task<RunResult> RunAsync(string source)
    {
        var (success, data, errors) = await PostAsync<RunData>("/run", source).ConfigureAwait(false);
        return new RunResult { Success = success, Data = data, Errors = errors };
    }

    public async Task<TraceResult> TraceAsync(string source)
    {
        var (success, data, errors) = await PostAsync<TraceData>("/trace", source).ConfigureAwait(false);
        return new TraceResult { Success = success, Data = data, Errors = errors };
    }

    private async Task<(bool Success, TData? Data, IReadOnlyList<UiError> Errors)> PostAsync<TData>(string endpoint, string source)
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
            // client-side. ERR_TRANSPORT is not in api.md §10's table because the
            // server never had a chance to classify the failure.
            return (false, default, [TransportError($"Could not reach the Limelight-X server: {ex.Message}")]);
        }

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Envelope<TData>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<Envelope<TData>>(body, PipelineJsonOptions.Default);
        }
        catch (JsonException)
        {
            envelope = null;
        }

        // Per api.md §10, the envelope shape is identical regardless of HTTP
        // status - even malformed-request 400s carry a standard envelope. A
        // null envelope here means the response body itself wasn't valid JSON
        // matching that shape at all, which the spec does not anticipate.
        if (envelope is null)
        {
            return (false, default, [TransportError($"Malformed response from server (HTTP {(int)response.StatusCode}).")]);
        }

        return (envelope.Success, envelope.Data, envelope.Errors);
    }

    private static ApiError TransportError(string message) => new()
    {
        Code = "ERR_TRANSPORT",
        Message = message,
        Severity = ErrorSeverity.Fatal,
        Category = ErrorCategory.Api,
    };

    public void Dispose() => _httpClient.Dispose();
}
