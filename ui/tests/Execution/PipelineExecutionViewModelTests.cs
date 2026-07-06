using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.Tests.TestDoubles;
using LimelightX.UI.ViewModels;
using LimelightX.UI.ViewModels.Errors;
using Xunit;

namespace LimelightX.UI.Tests.Execution;

public class PipelineExecutionViewModelTests
{
    private sealed class FakePipelineService : IPipelineService
    {
        public PipelineStartResult ExplainResultToReturn { get; set; } = new() { Accepted = true, CorrelationId = "corr-explain" };
        public PipelineStartResult RunResultToReturn { get; set; } = new() { Accepted = true, CorrelationId = "corr-run" };
        public PipelineStartResult TraceResultToReturn { get; set; } = new() { Accepted = true, CorrelationId = "corr-trace" };

        public Task<PipelineStartResult> ExplainAsync(string source) => Task.FromResult(ExplainResultToReturn);

        public Task<PipelineStartResult> RunAsync(string source) => Task.FromResult(RunResultToReturn);

        public Task<PipelineStartResult> TraceAsync(string source) => Task.FromResult(TraceResultToReturn);
    }

    private static AstNode MakeRoot() => new()
    {
        Type = "Program",
        Value = string.Empty,
        Span = new Span(),
        Metadata = new AstNodeMetadata(),
        Children = [],
    };

    [Fact]
    public async Task RunPipelineAsync_Accepted_SetsIsRunningAndNavigates()
    {
        var pipeline = new FakePipelineService();
        var viewModel = new PipelineExecutionViewModel(pipeline, new FakeEventStreamService());

        var outcome = await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");

        Assert.Equal(PipelineCallOutcome.NavigateToExecution, outcome);
        Assert.True(viewModel.IsRunning);
        Assert.True(viewModel.HasResult);
    }

    [Fact]
    public async Task RunPipelineAsync_AckPhaseRejected_ReturnsBlocked()
    {
        var pipeline = new FakePipelineService
        {
            RunResultToReturn = new PipelineStartResult
            {
                Accepted = false,
                Errors = [new UiError { Code = "ERR_TRANSPORT", Message = "unreachable", Severity = ErrorSeverity.Fatal, Category = ErrorCategory.Transport }],
            },
        };
        var viewModel = new PipelineExecutionViewModel(pipeline, new FakeEventStreamService());

        var outcome = await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");

        Assert.Equal(PipelineCallOutcome.Blocked, outcome);
        Assert.False(viewModel.HasResult);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task RunPipeline_FinalResultReadyEvent_PopulatesFinalResultAndClearsIsRunning()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService { RunResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-run" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream);

        await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "final_result_ready",
            "corr-run",
            new RunData { FinalResult = new FinalResult { Text = "the answer", ContentType = ResultContentType.Plain } }));

        Assert.Equal("the answer", viewModel.FinalResultViewModel.ResultText);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task ExplainPipeline_StreamsRawAndNormalizedAst_TerminatesAtNormalizedAst()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService { ExplainResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-explain" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream);

        var outcome = await viewModel.ExplainPipelineAsync("Load the article from \"a.txt\".");
        Assert.Equal(PipelineCallOutcome.NavigateToExecution, outcome);

        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-explain"));
        Assert.True(viewModel.IsRunning);

        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "raw_ast_generated",
            "corr-explain",
            new RawAstEventData { RawAst = new RawAstResponse { Root = MakeRoot(), RawText = "raw", Metadata = new AstMetadata { NodeCount = 1 } } }));
        Assert.NotNull(viewModel.RawAstViewModel.Tree);
        Assert.True(viewModel.IsRunning);

        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "normalized_ast_generated",
            "corr-explain",
            new NormalizedAstEventData { NormalizedAst = new NormalizedAstResponse { Root = MakeRoot(), RawText = "norm", Metadata = new NormalizedAstMetadata { NodeCount = 1 } } }));

        Assert.NotNull(viewModel.NormalizedAstViewModel.Tree);
        // /explain never evaluates - normalized_ast_generated is its terminal event.
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task TracePipeline_StreamsAllStagesInOrder_DerivesFinalResultFromFinalResultReadyEvent()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService { TraceResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-trace" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream);

        await viewModel.TracePipelineAsync("Load the article from \"a.txt\".\nSummarize it.");

        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-trace"));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "raw_ast_generated", "corr-trace",
            new RawAstEventData { RawAst = new RawAstResponse { Root = MakeRoot(), RawText = "raw", Metadata = new AstMetadata() } }));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "normalized_ast_generated", "corr-trace",
            new NormalizedAstEventData { NormalizedAst = new NormalizedAstResponse { Root = MakeRoot(), RawText = "norm", Metadata = new NormalizedAstMetadata() } }));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "ir_generated", "corr-trace",
            new IrEventData { Ir = new IrResponse { RawText = "ir", Metadata = new IrMetadata() } }));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "prompts_generated", "corr-trace",
            new PromptsEventData { Prompts = [new PromptBlock { OperationIndex = 0, PromptText = "Summarize this", Metadata = new PromptBlockMetadata() }] }));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "model_outputs_generated", "corr-trace",
            new ModelOutputsEventData
            {
                ModelOutputs =
                [
                    new ModelOutputBlock { OperationIndex = 0, RawText = "first output", ContentType = ResultContentType.Plain, Parsed = new ParsedContent(), Metadata = new ModelOutputMetadata() },
                    new ModelOutputBlock { OperationIndex = 1, RawText = "final output", ContentType = ResultContentType.Markdown, Parsed = new ParsedContent(), Metadata = new ModelOutputMetadata() },
                ],
            }));
        Assert.True(viewModel.IsRunning);

        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "final_result_ready", "corr-trace",
            new RunData { FinalResult = new FinalResult { Text = "final output", ContentType = ResultContentType.Markdown } }));

        Assert.False(viewModel.IsRunning);
        Assert.Equal("final output", viewModel.FinalResultViewModel.ResultText);
        Assert.Equal(LimelightX.UI.ViewModels.Inspectors.ResultContentType.Markdown, viewModel.FinalResultViewModel.ContentType);
        Assert.Single(viewModel.PromptViewModel.Prompts);
        Assert.Equal(2, viewModel.ModelOutputViewModel.Outputs.Count);
    }

    [Fact]
    public async Task PipelineFailedEvent_DistributesErrorToMatchingInspectorAndBanner()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService { ExplainResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-fail" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream);

        await viewModel.ExplainPipelineAsync("Summarize them.");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-fail"));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "pipeline_failed",
            "corr-fail",
            success: false,
            errors: [new UiError { Code = "ERR_CNL_NORMALIZE", Message = "bad pronoun", Severity = ErrorSeverity.Error, Category = ErrorCategory.Pipeline }]));

        Assert.False(viewModel.IsRunning);
        Assert.True(viewModel.HasErrors);
        Assert.Single(viewModel.NormalizedAstViewModel.Errors);
        Assert.Single(viewModel.Errors);
    }

    [Fact]
    public async Task MismatchedCorrelationId_EventIsIgnored()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService { RunResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-active" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream);

        await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "final_result_ready",
            "corr-stale",
            new RunData { FinalResult = new FinalResult { Text = "stale", ContentType = ResultContentType.Plain } }));

        Assert.True(viewModel.IsRunning);
        Assert.Equal(string.Empty, viewModel.FinalResultViewModel.ResultText);
    }
}
