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
        public PipelineStartResult TraceResultToReturn { get; set; } = new() { Accepted = true, CorrelationId = "corr-run" };

        public Task<PipelineStartResult> ExplainAsync(string source) => Task.FromResult(ExplainResultToReturn);

        public Task<PipelineStartResult> RunAsync(string source) => throw new NotImplementedException();

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
    public async Task RunPipelineAsync_Accepted_SetsIsRunningOncePipelineStartedArrives()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService();
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream, new ExecutionLockService());

        await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");
        Assert.False(viewModel.IsRunning);

        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));

        Assert.True(viewModel.IsRunning);
    }

    [Fact]
    public async Task RunPipelineAsync_AckPhaseRejected_PopulatesErrorsAndNeverAcquiresLock()
    {
        var executionLock = new ExecutionLockService();
        var pipeline = new FakePipelineService
        {
            TraceResultToReturn = new PipelineStartResult
            {
                Accepted = false,
                Errors = [new UiError { Code = "ERR_TRANSPORT", Message = "unreachable", Severity = ErrorSeverity.Fatal, Category = ErrorCategory.Transport }],
            },
        };
        var viewModel = new PipelineExecutionViewModel(pipeline, new FakeEventStreamService(), executionLock);

        await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");

        Assert.Single(viewModel.Errors);
        Assert.True(viewModel.HasErrors);
        Assert.False(viewModel.IsRunning);
        Assert.False(executionLock.IsAnyExecutionRunning);
    }

    [Fact]
    public async Task RunPipeline_FinalResultReadyEvent_PopulatesFinalResultAndClearsIsRunningAndReleasesLock()
    {
        var eventStream = new FakeEventStreamService();
        var executionLock = new ExecutionLockService();
        var pipeline = new FakePipelineService { TraceResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-run" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream, executionLock);

        await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        Assert.True(executionLock.IsAnyExecutionRunning);

        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "final_result_ready",
            "corr-run",
            new RunData { FinalResult = new FinalResult { Text = "the answer", ContentType = ResultContentType.Plain } }));

        Assert.Equal("the answer", viewModel.FinalResultViewModel.ResultText);
        Assert.False(viewModel.IsRunning);
        Assert.False(executionLock.IsAnyExecutionRunning);
    }

    [Fact]
    public async Task ExplainPipeline_StreamsRawAndNormalizedAst_TerminatesAtNormalizedAstAndReleasesLock()
    {
        var eventStream = new FakeEventStreamService();
        var executionLock = new ExecutionLockService();
        var pipeline = new FakePipelineService { ExplainResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-explain" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream, executionLock);

        await viewModel.ExplainPipelineAsync("Load the article from \"a.txt\".");

        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-explain"));
        Assert.True(viewModel.IsRunning);
        Assert.True(executionLock.IsAnyExecutionRunning);

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
        Assert.False(executionLock.IsAnyExecutionRunning);
    }

    [Fact]
    public async Task RunPipeline_StreamsAllSixStagesInOrder_SinceRunNowInvokesTrace()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService { TraceResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-run" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream, new ExecutionLockService());

        await viewModel.RunPipelineAsync("Load the article from \"a.txt\".\nSummarize it.");

        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "raw_ast_generated", "corr-run",
            new RawAstEventData { RawAst = new RawAstResponse { Root = MakeRoot(), RawText = "raw", Metadata = new AstMetadata() } }));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "normalized_ast_generated", "corr-run",
            new NormalizedAstEventData { NormalizedAst = new NormalizedAstResponse { Root = MakeRoot(), RawText = "norm", Metadata = new NormalizedAstMetadata() } }));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "ir_generated", "corr-run",
            new IrEventData { Ir = new IrResponse { RawText = "ir", Metadata = new IrMetadata() } }));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "prompt_generated", "corr-run",
            new PromptEventData { Prompt = new PromptBlock { OperationIndex = 0, PromptText = "Summarize this", Metadata = new PromptBlockMetadata() } }));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "model_output_generated", "corr-run",
            new ModelOutputEventData { ModelOutput = new ModelOutputBlock { OperationIndex = 0, RawText = "first output", ContentType = ResultContentType.Plain, Parsed = new ParsedContent(), Metadata = new ModelOutputMetadata() } }));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "model_output_generated", "corr-run",
            new ModelOutputEventData { ModelOutput = new ModelOutputBlock { OperationIndex = 1, RawText = "final output", ContentType = ResultContentType.Markdown, Parsed = new ParsedContent(), Metadata = new ModelOutputMetadata() } }));
        Assert.True(viewModel.IsRunning);

        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "final_result_ready", "corr-run",
            new RunData { FinalResult = new FinalResult { Text = "final output", ContentType = ResultContentType.Markdown } }));

        Assert.False(viewModel.IsRunning);
        Assert.Equal("final output", viewModel.FinalResultViewModel.ResultText);
        Assert.Equal(LimelightX.UI.ViewModels.Inspectors.ResultContentType.Markdown, viewModel.FinalResultViewModel.ContentType);
        Assert.Single(viewModel.PromptViewModel.Prompts);
        Assert.Equal(2, viewModel.ModelOutputViewModel.Outputs.Count);
        Assert.True(viewModel.PromptViewModel.HasPrompts);
        Assert.True(viewModel.ModelOutputViewModel.HasOutputs);
    }

    [Fact]
    public async Task PromptGenerated_SetsIsAwaitingModelOutput_ClearedOnModelOutputGenerated()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService { TraceResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-run" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream, new ExecutionLockService());

        await viewModel.RunPipelineAsync("Load the article from \"a.txt\".\nSummarize it.");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        Assert.False(viewModel.IsAwaitingModelOutput);

        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "prompt_generated", "corr-run",
            new PromptEventData { Prompt = new PromptBlock { OperationIndex = 0, PromptText = "Summarize this", Metadata = new PromptBlockMetadata() } }));
        Assert.True(viewModel.IsAwaitingModelOutput);

        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "model_output_generated", "corr-run",
            new ModelOutputEventData { ModelOutput = new ModelOutputBlock { OperationIndex = 0, RawText = "output", ContentType = ResultContentType.Plain, Parsed = new ParsedContent(), Metadata = new ModelOutputMetadata() } }));
        Assert.False(viewModel.IsAwaitingModelOutput);
    }

    [Fact]
    public async Task PipelineFailedEvent_ClearsIsAwaitingModelOutput()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService { TraceResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-run" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream, new ExecutionLockService());

        await viewModel.RunPipelineAsync("Load the article from \"a.txt\".\nSummarize it.");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "prompt_generated", "corr-run",
            new PromptEventData { Prompt = new PromptBlock { OperationIndex = 0, PromptText = "Summarize this", Metadata = new PromptBlockMetadata() } }));
        Assert.True(viewModel.IsAwaitingModelOutput);

        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "pipeline_failed",
            "corr-run",
            success: false,
            errors: [new UiError { Code = "ERR_MODEL_ADAPTER", Message = "model call failed", Severity = ErrorSeverity.Fatal, Category = ErrorCategory.Pipeline }]));

        Assert.False(viewModel.IsAwaitingModelOutput);
    }

    [Fact]
    public async Task PipelineFailedEvent_DistributesErrorToMatchingInspectorAndBannerAndReleasesLock()
    {
        var eventStream = new FakeEventStreamService();
        var executionLock = new ExecutionLockService();
        var pipeline = new FakePipelineService { ExplainResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-fail" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream, executionLock);

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
        Assert.False(executionLock.IsAnyExecutionRunning);
    }

    [Fact]
    public async Task MismatchedCorrelationId_EventIsIgnored()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService { TraceResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-active" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream, new ExecutionLockService());

        await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-active"));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "final_result_ready",
            "corr-stale",
            new RunData { FinalResult = new FinalResult { Text = "stale", ContentType = ResultContentType.Plain } }));

        Assert.True(viewModel.IsRunning);
        Assert.Equal(string.Empty, viewModel.FinalResultViewModel.ResultText);
    }

    [Fact]
    public async Task SecondTabRunPipelineAsync_WhileFirstTabHoldsLock_NeverAcquiresLock()
    {
        var executionLock = new ExecutionLockService();
        var eventStreamA = new FakeEventStreamService();
        var tabA = new PipelineExecutionViewModel(
            new FakePipelineService { TraceResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-a" } },
            eventStreamA,
            executionLock);
        await tabA.RunPipelineAsync("Load the article from \"a.txt\".");
        eventStreamA.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-a"));
        Assert.True(executionLock.IsAnyExecutionRunning);

        var eventStreamB = new FakeEventStreamService();
        var tabB = new PipelineExecutionViewModel(
            new FakePipelineService { TraceResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-b" } },
            eventStreamB,
            executionLock);
        await tabB.RunPipelineAsync("Load the article from \"b.txt\".");
        eventStreamB.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-b"));

        // Tab A's earlier TryAcquire already holds the lock - tab B's own
        // pipeline_started still marks tab B IsRunning locally (its request
        // was genuinely accepted and streaming), but the app-wide lock stays
        // held by tab A, matching ui-viewmodels.md §8: exactly one tab may
        // hold the lock at a time.
        Assert.True(tabB.IsRunning);
        Assert.True(executionLock.IsAnyExecutionRunning);

        eventStreamA.Raise(FakeEventStreamService.MakeEvent(
            "final_result_ready", "corr-a", new RunData { FinalResult = new FinalResult { Text = "a", ContentType = ResultContentType.Plain } }));

        Assert.False(executionLock.IsAnyExecutionRunning);
    }

    [Fact]
    public async Task Dispose_WhileRunning_ReleasesLockImmediately()
    {
        var executionLock = new ExecutionLockService();
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService { TraceResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-run" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream, executionLock);

        await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        Assert.True(executionLock.IsAnyExecutionRunning);

        viewModel.Dispose();

        Assert.False(executionLock.IsAnyExecutionRunning);
    }

    [Fact]
    public async Task Dispose_WhileRunning_StopsReactingToLateEvents()
    {
        var executionLock = new ExecutionLockService();
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService { TraceResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-run" } };
        var viewModel = new PipelineExecutionViewModel(pipeline, eventStream, executionLock);

        await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");
        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-run"));
        viewModel.Dispose();

        // A late event arriving after disposal must not be applied - the
        // disposed tab's ViewModel state should be frozen as of Dispose().
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "final_result_ready", "corr-run", new RunData { FinalResult = new FinalResult { Text = "late", ContentType = ResultContentType.Plain } }));

        Assert.Equal(string.Empty, viewModel.FinalResultViewModel.ResultText);
    }
}
