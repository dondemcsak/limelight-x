using System.Net.Sockets;
using System.Text.Json;
using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using Xunit;

namespace LimelightX.UI.Tests.Services;

/// <summary>
/// Phase 2 "Done" criterion: manually verify PipelineService/EventStreamService
/// against a real running `llx serve` instance to confirm the snake_case +
/// case-insensitive-enum deserialization strategy against the actual server,
/// not just assumptions about the wire shape.
///
/// Requires `llx serve --port 4747` running with ANTHROPIC_API_KEY set
/// (any value - /explain never calls the model). Not part of the regular
/// mock-only suite (ui-testing.md explicitly scopes that to mocked
/// PipelineService responses) - this is a one-off verification. Each test
/// self-skips (passes trivially) when no server is reachable on :4747, so
/// `dotnet test` stays green without a manually-started server; run
/// `llx serve --port 4747` first to get real coverage from these.
/// </summary>
public class PipelineServiceLiveTests
{
    private const string Source =
        "Load the transcript from \"meeting.txt\".\n" +
        "Extract the action items from the transcript.\n" +
        "Summarize the action items.\n" +
        "Rewrite the summary using {{ prompt: \"Rewrite in a friendly, conversational tone suitable for a Slack update.\" }}.\n";

    private static async Task<bool> IsServerReachableAsync()
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 4747).WaitAsync(TimeSpan.FromMilliseconds(300));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<WsEvent> WaitForEventAsync(EventStreamService eventStream, string correlationId, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<WsEvent>();
        void Handler(WsEvent e)
        {
            if (e.CorrelationId == correlationId && e.EventType is "final_result_ready" or "normalized_ast_generated" or "pipeline_failed")
            {
                tcs.TrySetResult(e);
            }
        }

        eventStream.EventReceived += Handler;
        try
        {
            return await tcs.Task.WaitAsync(timeout).ConfigureAwait(false);
        }
        finally
        {
            eventStream.EventReceived -= Handler;
        }
    }

    [Fact]
    public async Task ExplainAsync_AgainstRealServer_StreamsRawAndNormalizedAst()
    {
        if (!await IsServerReachableAsync())
        {
            return;
        }

        using var service = new PipelineService(4747);
        using var eventStream = new EventStreamService();
        await eventStream.ConnectAsync(4747, TestContext.Current.CancellationToken);

        var events = new List<WsEvent>();
        eventStream.EventReceived += events.Add;

        var ack = await service.ExplainAsync(Source);
        Assert.True(ack.Accepted, string.Join("; ", ack.Errors.Select(e => e.Message)));

        var terminal = await WaitForEventAsync(eventStream, ack.CorrelationId!, TimeSpan.FromSeconds(10));
        Assert.Equal("normalized_ast_generated", terminal.EventType);

        var rawAstEvent = events.Single(e => e.CorrelationId == ack.CorrelationId && e.EventType == "raw_ast_generated");
        var rawAst = rawAstEvent.Data!.Value.Deserialize<RawAstEventData>(PipelineJsonOptions.Default)!.RawAst;
        Assert.Equal(5, rawAst.Metadata.NodeCount);
        Assert.Equal("Program", rawAst.Root.Type);
        Assert.Equal(4, rawAst.Root.Children.Count);
        Assert.Equal("Load", rawAst.Root.Children[0].Type);

        var normalizedAst = terminal.Data!.Value.Deserialize<NormalizedAstEventData>(PipelineJsonOptions.Default)!.NormalizedAst;
        Assert.Equal(5, normalizedAst.Metadata.NodeCount);
        Assert.True(normalizedAst.Root.Children[3].Metadata.ExpressionHole);
    }

    [Fact]
    public async Task TraceAsync_AgainstRealServer_StreamsFatalPipelineError()
    {
        if (!await IsServerReachableAsync())
        {
            return;
        }

        using var service = new PipelineService(4747);
        using var eventStream = new EventStreamService();
        await eventStream.ConnectAsync(4747, TestContext.Current.CancellationToken);

        // "meeting.txt" is relative to the server process's CWD (repo root),
        // where it doesn't exist - this deliberately drives a fatal
        // evaluator error (ERR_EVALUATOR_FATAL, not a real model call) so the
        // test proves the fatal-error deserialization path without needing a
        // real ANTHROPIC_API_KEY.
        var ack = await service.TraceAsync(Source);
        Assert.True(ack.Accepted);

        var terminal = await WaitForEventAsync(eventStream, ack.CorrelationId!, TimeSpan.FromSeconds(10));

        Assert.Equal("pipeline_failed", terminal.EventType);
        Assert.False(terminal.Success);
        Assert.NotEmpty(terminal.Errors);
        Assert.Equal(ViewModels.Errors.ErrorSeverity.Fatal, terminal.Errors[0].Severity);
        Assert.Equal(ViewModels.Errors.ErrorCategory.Pipeline, terminal.Errors[0].Category);
    }
}
