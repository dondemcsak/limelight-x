using System.Net.Sockets;
using LimelightX.UI.Services;
using Xunit;

namespace LimelightX.UI.Tests.Services;

/// <summary>
/// Phase 2 "Done" criterion: manually verify PipelineService against a real
/// running `llx serve` instance to confirm the snake_case + case-insensitive-
/// enum deserialization strategy against the actual server, not just
/// assumptions about the wire shape.
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

    [Fact]
    public async Task ExplainAsync_AgainstRealServer_DeserializesCorrectly()
    {
        if (!await IsServerReachableAsync())
        {
            return;
        }

        using var service = new PipelineService(4747);

        var result = await service.ExplainAsync(Source);

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data!.RawAst.Metadata.NodeCount);
        Assert.Equal("Program", result.Data.RawAst.Root.Type);
        Assert.Equal(4, result.Data.RawAst.Root.Children.Count);
        Assert.Equal("Load", result.Data.RawAst.Root.Children[0].Type);
        Assert.Equal(5, result.Data.NormalizedAst.Metadata.NodeCount);
        Assert.True(result.Data.NormalizedAst.Root.Children[3].Metadata.ExpressionHole);
    }

    [Fact]
    public async Task TraceAsync_AgainstRealServer_DeserializesFatalPipelineError()
    {
        if (!await IsServerReachableAsync())
        {
            return;
        }

        using var service = new PipelineService(4747);

        // "meeting.txt" is relative to the server process's CWD (repo root),
        // where it doesn't exist - this deliberately drives a fatal
        // evaluator error (ERR_EVALUATOR_FATAL, not a real model call) so the
        // test proves the fatal-error deserialization path without needing a
        // real ANTHROPIC_API_KEY.
        var result = await service.TraceAsync(Source);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(ViewModels.Errors.ErrorSeverity.Fatal, result.Errors[0].Severity);
        Assert.Equal(ViewModels.Errors.ErrorCategory.Pipeline, result.Errors[0].Category);
    }
}
