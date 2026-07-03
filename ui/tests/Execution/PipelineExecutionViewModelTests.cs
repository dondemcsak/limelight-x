using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels;
using LimelightX.UI.ViewModels.Errors;
using Xunit;

namespace LimelightX.UI.Tests.Execution;

public class PipelineExecutionViewModelTests
{
    private sealed class FakePipelineService : IPipelineService
    {
        public ExplainResult ExplainResultToReturn { get; set; } = new() { Success = true };
        public RunResult RunResultToReturn { get; set; } = new() { Success = true };
        public TraceResult TraceResultToReturn { get; set; } = new() { Success = true };

        public Task<ExplainResult> ExplainAsync(string source) => Task.FromResult(ExplainResultToReturn);

        public Task<RunResult> RunAsync(string source) => Task.FromResult(RunResultToReturn);

        public Task<TraceResult> TraceAsync(string source) => Task.FromResult(TraceResultToReturn);
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
    public async Task RunPipelineAsync_Success_PopulatesFinalResultAndNavigates()
    {
        var pipeline = new FakePipelineService
        {
            RunResultToReturn = new RunResult
            {
                Success = true,
                Data = new RunData { FinalResult = new FinalResult { Text = "the answer", ContentType = ResultContentType.Plain } },
            },
        };
        var viewModel = new PipelineExecutionViewModel(pipeline);

        var outcome = await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");

        Assert.Equal(PipelineCallOutcome.NavigateToExecution, outcome);
        Assert.Equal("the answer", viewModel.FinalResultViewModel.ResultText);
        Assert.True(viewModel.HasResult);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task RunPipelineAsync_NoData_ReturnsBlocked()
    {
        var pipeline = new FakePipelineService
        {
            RunResultToReturn = new RunResult
            {
                Success = false,
                Data = null,
                Errors = [new UiError { Code = "ERR_TRANSPORT", Message = "unreachable", Severity = ErrorSeverity.Fatal, Category = ErrorCategory.Api }],
            },
        };
        var viewModel = new PipelineExecutionViewModel(pipeline);

        var outcome = await viewModel.RunPipelineAsync("Load the article from \"a.txt\".");

        Assert.Equal(PipelineCallOutcome.Blocked, outcome);
        Assert.False(viewModel.HasResult);
    }

    [Fact]
    public async Task ExplainPipelineAsync_Success_PopulatesRawAndNormalizedAst()
    {
        var pipeline = new FakePipelineService
        {
            ExplainResultToReturn = new ExplainResult
            {
                Success = true,
                Data = new ExplainData
                {
                    RawAst = new RawAstResponse { Root = MakeRoot(), RawText = "raw", Metadata = new AstMetadata { NodeCount = 1 } },
                    NormalizedAst = new NormalizedAstResponse { Root = MakeRoot(), RawText = "norm", Metadata = new NormalizedAstMetadata { NodeCount = 1 } },
                },
            },
        };
        var viewModel = new PipelineExecutionViewModel(pipeline);

        var outcome = await viewModel.ExplainPipelineAsync("Load the article from \"a.txt\".");

        Assert.Equal(PipelineCallOutcome.NavigateToExecution, outcome);
        Assert.NotNull(viewModel.RawAstViewModel.Tree);
        Assert.NotNull(viewModel.NormalizedAstViewModel.Tree);
        Assert.True(viewModel.HasResult);
    }

    [Fact]
    public async Task TracePipelineAsync_Success_DerivesFinalResultFromLastModelOutput()
    {
        var pipeline = new FakePipelineService
        {
            TraceResultToReturn = new TraceResult
            {
                Success = true,
                Data = new TraceData
                {
                    RawAst = new RawAstResponse { Root = MakeRoot(), RawText = "raw", Metadata = new AstMetadata() },
                    NormalizedAst = new NormalizedAstResponse { Root = MakeRoot(), RawText = "norm", Metadata = new NormalizedAstMetadata() },
                    Ir = new IrResponse { RawText = "ir", Metadata = new IrMetadata() },
                    Prompts =
                    [
                        new PromptBlock { OperationIndex = 0, PromptText = "Summarize this", Metadata = new PromptBlockMetadata() },
                    ],
                    ModelOutputs =
                    [
                        new ModelOutputBlock { OperationIndex = 0, RawText = "first output", ContentType = ResultContentType.Plain, Parsed = new ParsedContent(), Metadata = new ModelOutputMetadata() },
                        new ModelOutputBlock { OperationIndex = 1, RawText = "final output", ContentType = ResultContentType.Markdown, Parsed = new ParsedContent(), Metadata = new ModelOutputMetadata() },
                    ],
                },
            },
        };
        var viewModel = new PipelineExecutionViewModel(pipeline);

        var outcome = await viewModel.TracePipelineAsync("Load the article from \"a.txt\".\nSummarize it.");

        Assert.Equal(PipelineCallOutcome.NavigateToExecution, outcome);
        Assert.Equal("final output", viewModel.FinalResultViewModel.ResultText);
        Assert.Equal(LimelightX.UI.ViewModels.Inspectors.ResultContentType.Markdown, viewModel.FinalResultViewModel.ContentType);
        Assert.Single(viewModel.PromptViewModel.Prompts);
        Assert.Equal(2, viewModel.ModelOutputViewModel.Outputs.Count);
    }

    [Fact]
    public async Task ExplainPipelineAsync_PartialFailure_StillNavigatesAndAssignsErrorToRawAst()
    {
        var pipeline = new FakePipelineService
        {
            ExplainResultToReturn = new ExplainResult
            {
                Success = false,
                Data = new ExplainData
                {
                    RawAst = new RawAstResponse { Root = MakeRoot(), RawText = "raw", Metadata = new AstMetadata() },
                    NormalizedAst = new NormalizedAstResponse { Root = MakeRoot(), RawText = "norm", Metadata = new NormalizedAstMetadata() },
                },
                Errors = [new UiError { Code = "ERR_CNL_NORMALIZE", Message = "bad pronoun", Severity = ErrorSeverity.Error, Category = ErrorCategory.Pipeline }],
            },
        };
        var viewModel = new PipelineExecutionViewModel(pipeline);

        var outcome = await viewModel.ExplainPipelineAsync("Summarize them.");

        Assert.Equal(PipelineCallOutcome.NavigateToExecution, outcome);
        Assert.Single(viewModel.NormalizedAstViewModel.Errors);
        Assert.Single(viewModel.Errors);
    }
}
