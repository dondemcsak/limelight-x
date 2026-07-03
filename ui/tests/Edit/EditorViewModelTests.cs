using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels;
using LimelightX.UI.ViewModels.Errors;
using Xunit;

namespace LimelightX.UI.Tests.Edit;

public class EditorViewModelTests
{
    private sealed class FakePipelineService : IPipelineService
    {
        public ExplainResult ExplainResultToReturn { get; set; } = new() { Success = true };
        public int ExplainCallCount { get; private set; }

        public Task<ExplainResult> ExplainAsync(string source)
        {
            ExplainCallCount++;
            return Task.FromResult(ExplainResultToReturn);
        }

        public Task<RunResult> RunAsync(string source) => throw new NotImplementedException();

        public Task<TraceResult> TraceAsync(string source) => throw new NotImplementedException();
    }

    private static async Task WaitForDebounceAsync() => await Task.Delay(700);

    [Fact]
    public async Task TextChanged_InvalidCnl_PopulatesValidationErrorsAndBlocksCommands()
    {
        var pipeline = new FakePipelineService
        {
            ExplainResultToReturn = new ExplainResult
            {
                Success = false,
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
        var viewModel = new EditorViewModel(pipeline);

        viewModel.Text = "Load the article from \"a.txt\"";
        await WaitForDebounceAsync();

        Assert.Single(viewModel.ValidationErrors);
        Assert.Equal("ERR_CNL_PARSE", viewModel.ValidationErrors[0].Code);
        Assert.False(viewModel.RunCommand.CanExecute(null));
        Assert.False(viewModel.ExplainCommand.CanExecute(null));
        Assert.False(viewModel.TraceCommand.CanExecute(null));

        // ui-error-handling.md §9: error-or-higher severity also raises the global banner.
        Assert.Single(viewModel.Errors);
        Assert.Equal("ERR_CNL_PARSE", viewModel.Errors[0].Code);
    }

    [Fact]
    public async Task TextChanged_WarningSeverity_StaysInlineOnlyNotInBanner()
    {
        var pipeline = new FakePipelineService
        {
            ExplainResultToReturn = new ExplainResult
            {
                Success = true,
                Errors =
                [
                    new UiError
                    {
                        Code = "ERR_STYLE_HINT",
                        Message = "Consider rephrasing.",
                        Severity = ErrorSeverity.Warning,
                        Category = ErrorCategory.Validation,
                    },
                ],
            },
        };
        var viewModel = new EditorViewModel(pipeline);

        viewModel.Text = "Load the article from \"a.txt\".\nSummarize it.";
        await WaitForDebounceAsync();

        Assert.Single(viewModel.ValidationErrors);
        Assert.Empty(viewModel.Errors);
    }

    [Fact]
    public async Task TextChanged_ValidCnl_ClearsValidationErrorsAndEnablesCommands()
    {
        var pipeline = new FakePipelineService { ExplainResultToReturn = new ExplainResult { Success = true } };
        var viewModel = new EditorViewModel(pipeline);

        viewModel.Text = "Load the article from \"a.txt\".\nSummarize it.";
        await WaitForDebounceAsync();

        Assert.Empty(viewModel.ValidationErrors);
        Assert.True(viewModel.RunCommand.CanExecute(null));
        Assert.True(viewModel.ExplainCommand.CanExecute(null));
        Assert.True(viewModel.TraceCommand.CanExecute(null));
    }

    [Fact]
    public async Task TextChanged_EmptyText_DoesNotCallBackend()
    {
        var pipeline = new FakePipelineService();
        var viewModel = new EditorViewModel(pipeline);

        viewModel.Text = "   ";
        await WaitForDebounceAsync();

        Assert.Equal(0, pipeline.ExplainCallCount);
        Assert.Empty(viewModel.ValidationErrors);
    }

    [Fact]
    public async Task TextChanged_RapidTyping_OnlyValidatesOnceAfterDebounceSettles()
    {
        var pipeline = new FakePipelineService { ExplainResultToReturn = new ExplainResult { Success = true } };
        var viewModel = new EditorViewModel(pipeline);

        viewModel.Text = "L";
        viewModel.Text = "Lo";
        viewModel.Text = "Load the article from \"a.txt\".";
        await WaitForDebounceAsync();

        Assert.Equal(1, pipeline.ExplainCallCount);
    }

    [Fact]
    public void UndoCommand_RaisesUndoRequested()
    {
        var viewModel = new EditorViewModel(new FakePipelineService());
        var raised = false;
        viewModel.UndoRequested += () => raised = true;

        viewModel.UndoCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void RedoCommand_RaisesRedoRequested()
    {
        var viewModel = new EditorViewModel(new FakePipelineService());
        var raised = false;
        viewModel.RedoRequested += () => raised = true;

        viewModel.RedoCommand.Execute(null);

        Assert.True(raised);
    }
}
