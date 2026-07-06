using LimelightX.UI.Services;
using LimelightX.UI.Tests.TestDoubles;
using LimelightX.UI.ViewModels;
using LimelightX.UI.ViewModels.Errors;
using Xunit;

namespace LimelightX.UI.Tests.Edit;

public class EditorViewModelTests
{
    private sealed class FakePipelineService : IPipelineService
    {
        public PipelineStartResult ExplainResultToReturn { get; set; } = new() { Accepted = true, CorrelationId = "corr-0" };
        public int ExplainCallCount { get; private set; }

        public Task<PipelineStartResult> ExplainAsync(string source)
        {
            ExplainCallCount++;
            return Task.FromResult(ExplainResultToReturn);
        }

        public Task<PipelineStartResult> RunAsync(string source) => throw new NotImplementedException();

        public Task<PipelineStartResult> TraceAsync(string source) => throw new NotImplementedException();
    }

    private static async Task WaitForDebounceAsync() => await Task.Delay(700);

    [Fact]
    public async Task TextChanged_InvalidCnl_AckPhaseFailure_PopulatesValidationErrorsButDoesNotBlockCommands()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService
        {
            ExplainResultToReturn = new PipelineStartResult
            {
                Accepted = false,
                Errors =
                [
                    new UiError
                    {
                        Code = "ERR_CNL_PARSE",
                        Message = "Missing period.",
                        Severity = ErrorSeverity.Error,
                        Category = ErrorCategory.Pipeline,
                    },
                ],
            },
        };
        var viewModel = new EditorViewModel(pipeline, eventStream, new ExecutionLockService());

        viewModel.Text = "Load the article from \"a.txt\"";
        await WaitForDebounceAsync();

        Assert.Single(viewModel.ValidationErrors);
        Assert.Equal("ERR_CNL_PARSE", viewModel.ValidationErrors[0].Code);

        // ui-viewmodels.md §6: CanExecute no longer checks ValidationErrors -
        // known validation errors no longer block Run/Explain client-side
        // (confirmed decision, see the approved migration plan).
        Assert.True(viewModel.RunCommand.CanExecute(null));
        Assert.True(viewModel.ExplainCommand.CanExecute(null));

        // ui-error-handling.md §9: error-or-higher severity also raises the global banner.
        Assert.Single(viewModel.Errors);
        Assert.Equal("ERR_CNL_PARSE", viewModel.Errors[0].Code);
    }

    [Fact]
    public async Task TextChanged_PipelineFailedEvent_PopulatesValidationErrors()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService
        {
            ExplainResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-1" },
        };
        var viewModel = new EditorViewModel(pipeline, eventStream, new ExecutionLockService());

        viewModel.Text = "Summarize them.";
        await WaitForDebounceAsync();

        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-1"));
        eventStream.Raise(FakeEventStreamService.MakeEvent(
            "pipeline_failed",
            "corr-1",
            success: false,
            errors: [new UiError { Code = "ERR_CNL_NORMALIZE", Message = "bad pronoun", Severity = ErrorSeverity.Error, Category = ErrorCategory.Pipeline }]));

        Assert.Single(viewModel.ValidationErrors);
        Assert.Equal("ERR_CNL_NORMALIZE", viewModel.ValidationErrors[0].Code);
        Assert.False(viewModel.IsValidating);
    }

    [Fact]
    public async Task TextChanged_NormalizedAstGeneratedEvent_ClearsValidatingWithNoErrors()
    {
        var eventStream = new FakeEventStreamService();
        var pipeline = new FakePipelineService
        {
            ExplainResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-2" },
        };
        var viewModel = new EditorViewModel(pipeline, eventStream, new ExecutionLockService());

        viewModel.Text = "Load the article from \"a.txt\".\nSummarize it.";
        await WaitForDebounceAsync();

        eventStream.Raise(FakeEventStreamService.MakeEvent("pipeline_started", "corr-2"));
        eventStream.Raise(FakeEventStreamService.MakeEvent("normalized_ast_generated", "corr-2"));

        Assert.Empty(viewModel.ValidationErrors);
        Assert.False(viewModel.IsValidating);
        Assert.True(viewModel.RunCommand.CanExecute(null));
        Assert.True(viewModel.ExplainCommand.CanExecute(null));
    }

    [Fact]
    public async Task TextChanged_EmptyText_DoesNotCallBackend()
    {
        var pipeline = new FakePipelineService();
        var viewModel = new EditorViewModel(pipeline, new FakeEventStreamService(), new ExecutionLockService());

        viewModel.Text = "   ";
        await WaitForDebounceAsync();

        Assert.Equal(0, pipeline.ExplainCallCount);
        Assert.Empty(viewModel.ValidationErrors);
    }

    [Fact]
    public async Task TextChanged_RapidTyping_OnlyValidatesOnceAfterDebounceSettles()
    {
        var pipeline = new FakePipelineService { ExplainResultToReturn = new PipelineStartResult { Accepted = true, CorrelationId = "corr-3" } };
        var viewModel = new EditorViewModel(pipeline, new FakeEventStreamService(), new ExecutionLockService());

        viewModel.Text = "L";
        viewModel.Text = "Lo";
        viewModel.Text = "Load the article from \"a.txt\".";
        await WaitForDebounceAsync();

        Assert.Equal(1, pipeline.ExplainCallCount);
    }

    [Fact]
    public void RunExplainCommands_BlockedWhileAnyTabExecutionRunning()
    {
        var pipeline = new FakePipelineService();
        var executionLock = new ExecutionLockService();
        executionLock.TryAcquire(new object());
        var viewModel = new EditorViewModel(pipeline, new FakeEventStreamService(), executionLock)
        {
            Text = "Load the article from \"a.txt\".",
        };

        Assert.False(viewModel.RunCommand.CanExecute(null));
        Assert.False(viewModel.ExplainCommand.CanExecute(null));
    }

    [Fact]
    public void RunExplainCommands_BlockedWhileTextEmpty()
    {
        var pipeline = new FakePipelineService();
        var viewModel = new EditorViewModel(pipeline, new FakeEventStreamService(), new ExecutionLockService());

        Assert.False(viewModel.RunCommand.CanExecute(null));
        Assert.False(viewModel.ExplainCommand.CanExecute(null));
    }

    [Fact]
    public void RunExplainCommands_ReenableWhenExecutionLockReleases()
    {
        var pipeline = new FakePipelineService();
        var executionLock = new ExecutionLockService();
        var token = new object();
        executionLock.TryAcquire(token);
        var viewModel = new EditorViewModel(pipeline, new FakeEventStreamService(), executionLock)
        {
            Text = "Load the article from \"a.txt\".",
        };
        Assert.False(viewModel.RunCommand.CanExecute(null));

        executionLock.Release(token);

        Assert.True(viewModel.RunCommand.CanExecute(null));
        Assert.True(viewModel.ExplainCommand.CanExecute(null));
    }

    [Fact]
    public void UndoCommand_RaisesUndoRequested()
    {
        var viewModel = new EditorViewModel(new FakePipelineService(), new FakeEventStreamService(), new ExecutionLockService());
        var raised = false;
        viewModel.UndoRequested += () => raised = true;

        viewModel.UndoCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void RedoCommand_RaisesRedoRequested()
    {
        var viewModel = new EditorViewModel(new FakePipelineService(), new FakeEventStreamService(), new ExecutionLockService());
        var raised = false;
        viewModel.RedoRequested += () => raised = true;

        viewModel.RedoCommand.Execute(null);

        Assert.True(raised);
    }
}
