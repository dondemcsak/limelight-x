using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;
using LimelightX.UI.ViewModels.Inspectors;

namespace LimelightX.UI.ViewModels;

/// <summary>
/// Coordinates pipeline execution and inspector ViewModels (ui-viewmodels.md
/// §5.1). Triggered by the composition root in response to EditorViewModel's
/// Run/Explain/TraceRequested events, not bound to directly from any View -
/// EditorPage's buttons bind to EditorViewModel's own commands.
///
/// Note on the "partial failure" branches below (DistributeErrorsToInspectors
/// etc.): confirmed via direct curl against the real server that today's
/// backend always omits `data` entirely on any pipeline failure (parse,
/// normalize, and evaluator-fatal all tested) - never success:false with
/// partial data alongside it. Those branches are therefore currently
/// unreachable in practice, but are kept as written because they match
/// bdd-ui-navigation.md's stated intent (a partial pipeline failure should
/// still navigate and show inline/banner errors with whatever data exists)
/// and cost nothing to leave in place for if/when the backend changes.
/// </summary>
public partial class PipelineExecutionViewModel : ObservableObject
{
    private readonly IPipelineService _pipelineService;

    public PipelineExecutionViewModel(IPipelineService pipelineService)
    {
        _pipelineService = pipelineService;
    }

    public RawAstViewModel RawAstViewModel { get; } = new();

    public NormalizedAstViewModel NormalizedAstViewModel { get; } = new();

    public IrViewModel IrViewModel { get; } = new();

    public PromptViewModel PromptViewModel { get; } = new();

    public ModelOutputViewModel ModelOutputViewModel { get; } = new();

    public FinalResultViewModel FinalResultViewModel { get; } = new();

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isTracing;

    [ObservableProperty]
    private bool _isExplaining;

    public ObservableCollection<UiError> Errors { get; } = [];

    /// <summary>
    /// ui-routing-navigation.md §9: sidebar cannot navigate to Execution
    /// unless a pipeline has produced a result. Set whenever any call
    /// returns inspector data (even a partial pipeline failure still counts,
    /// since ExecutionPage has content to show either way).
    /// </summary>
    public bool HasResult { get; private set; }

    public async Task<PipelineCallOutcome> RunPipelineAsync(string source)
    {
        IsRunning = true;
        Errors.Clear();
        try
        {
            var result = await _pipelineService.RunAsync(source);
            return ApplyRunResult(result);
        }
        finally
        {
            IsRunning = false;
        }
    }

    public async Task<PipelineCallOutcome> ExplainPipelineAsync(string source)
    {
        IsExplaining = true;
        Errors.Clear();
        try
        {
            var result = await _pipelineService.ExplainAsync(source);
            return ApplyExplainResult(result);
        }
        finally
        {
            IsExplaining = false;
        }
    }

    public async Task<PipelineCallOutcome> TracePipelineAsync(string source)
    {
        IsTracing = true;
        Errors.Clear();
        try
        {
            var result = await _pipelineService.TraceAsync(source);
            return ApplyTraceResult(result);
        }
        finally
        {
            IsTracing = false;
        }
    }

    private PipelineCallOutcome ApplyRunResult(RunResult result)
    {
        RawAstViewModel.Reset();
        NormalizedAstViewModel.Reset();
        IrViewModel.Reset();
        PromptViewModel.Reset();
        ModelOutputViewModel.Reset();
        FinalResultViewModel.Reset();

        Errors.Clear();
        foreach (var error in result.Errors)
        {
            Errors.Add(error);
        }

        if (result.Data is null)
        {
            return PipelineCallOutcome.Blocked;
        }

        FinalResultViewModel.ResultText = result.Data.FinalResult.Text;
        FinalResultViewModel.RawText = result.Data.FinalResult.Text;
        FinalResultViewModel.ContentType = MapContentType(result.Data.FinalResult.ContentType);

        if (!result.Success)
        {
            AddErrorsTo(FinalResultViewModel.Errors, result.Errors);
        }

        HasResult = true;
        return PipelineCallOutcome.NavigateToExecution;
    }

    private PipelineCallOutcome ApplyExplainResult(ExplainResult result)
    {
        RawAstViewModel.Reset();
        NormalizedAstViewModel.Reset();

        Errors.Clear();
        foreach (var error in result.Errors)
        {
            Errors.Add(error);
        }

        if (result.Data is null)
        {
            return PipelineCallOutcome.Blocked;
        }

        ApplyRawAst(result.Data.RawAst);
        ApplyNormalizedAst(result.Data.NormalizedAst);

        if (!result.Success)
        {
            DistributeErrorsToInspectors(result.Errors);
        }

        HasResult = true;
        return PipelineCallOutcome.NavigateToExecution;
    }

    private PipelineCallOutcome ApplyTraceResult(TraceResult result)
    {
        RawAstViewModel.Reset();
        NormalizedAstViewModel.Reset();
        IrViewModel.Reset();
        PromptViewModel.Reset();
        ModelOutputViewModel.Reset();
        FinalResultViewModel.Reset();

        Errors.Clear();
        foreach (var error in result.Errors)
        {
            Errors.Add(error);
        }

        if (result.Data is null)
        {
            return PipelineCallOutcome.Blocked;
        }

        ApplyRawAst(result.Data.RawAst);
        ApplyNormalizedAst(result.Data.NormalizedAst);

        IrViewModel.RawText = result.Data.Ir.RawText;
        IrViewModel.Metadata = result.Data.Ir.Metadata;
        foreach (var operation in result.Data.Ir.Operations)
        {
            IrViewModel.Operations.Add(operation);
        }

        foreach (var prompt in result.Data.Prompts)
        {
            PromptViewModel.Prompts.Add(prompt);
        }

        foreach (var output in result.Data.ModelOutputs)
        {
            ModelOutputViewModel.Outputs.Add(output);
        }

        // /trace has no final_result field (confirmed against tests/api_trace.rs) -
        // derive it from the last model output, per the documented spec
        // discrepancy (spec/api.md §2.1 implies one, the wire contract doesn't).
        var lastOutput = result.Data.ModelOutputs.Count > 0 ? result.Data.ModelOutputs[^1] : null;
        if (lastOutput is not null)
        {
            FinalResultViewModel.ResultText = lastOutput.RawText;
            FinalResultViewModel.RawText = lastOutput.RawText;
            FinalResultViewModel.ContentType = MapContentType(lastOutput.ContentType);
        }

        if (!result.Success)
        {
            DistributeErrorsToInspectors(result.Errors);
        }

        HasResult = true;
        return PipelineCallOutcome.NavigateToExecution;
    }

    private void ApplyRawAst(RawAstResponse rawAst)
    {
        RawAstViewModel.Tree = rawAst.Root;
        RawAstViewModel.RawText = rawAst.RawText;
        RawAstViewModel.Metadata = rawAst.Metadata;
    }

    private void ApplyNormalizedAst(NormalizedAstResponse normalizedAst)
    {
        NormalizedAstViewModel.Tree = normalizedAst.Root;
        NormalizedAstViewModel.RawText = normalizedAst.RawText;
        NormalizedAstViewModel.Metadata = normalizedAst.Metadata;
    }

    /// <summary>
    /// Assigns each error to the inspector matching its pipeline stage
    /// (deterministic code-based mapping, not a heuristic guess) in addition
    /// to the page-level Errors banner collection populated above.
    /// </summary>
    private void DistributeErrorsToInspectors(IReadOnlyList<UiError> errors)
    {
        foreach (var error in errors)
        {
            var target = error.Code switch
            {
                "ERR_CNL_PARSE" => RawAstViewModel.Errors,
                "ERR_CNL_NORMALIZE" => NormalizedAstViewModel.Errors,
                "ERR_IR_COMPILE" => IrViewModel.Errors,
                "ERR_EVALUATOR_FATAL" or "ERR_MODEL_ADAPTER" => ModelOutputViewModel.Errors,
                _ => null,
            };

            target?.Add(error);
        }
    }

    private static void AddErrorsTo(ObservableCollection<UiError> target, IReadOnlyList<UiError> errors)
    {
        foreach (var error in errors)
        {
            target.Add(error);
        }
    }

    private static Inspectors.ResultContentType MapContentType(Services.Dto.ResultContentType wireType) => wireType switch
    {
        Services.Dto.ResultContentType.Markdown => Inspectors.ResultContentType.Markdown,
        Services.Dto.ResultContentType.Json => Inspectors.ResultContentType.Json,
        _ => Inspectors.ResultContentType.PlainText,
    };
}
