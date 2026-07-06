using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;
using LimelightX.UI.ViewModels.Inspectors;

namespace LimelightX.UI.ViewModels;

/// <summary>
/// Which endpoint the active correlation_id belongs to - needed because
/// /explain's event sequence ends at normalized_ast_generated (no
/// final_result_ready, since it never invokes the evaluator), while /run and
/// /trace both end at final_result_ready (api.md §2.1).
/// </summary>
internal enum PipelineJobKind
{
    Run,
    Explain,
    Trace,
}

/// <summary>
/// Coordinates pipeline execution and inspector ViewModels (ui-viewmodels.md
/// §6). Triggered by the composition root in response to EditorViewModel's
/// Run/Explain/TraceRequested events, not bound to directly from any View -
/// EditorPage's buttons bind to EditorViewModel's own commands.
///
/// Unlike the old single-response model, Run/Explain/TracePipelineAsync only
/// await the immediate ack; actual stage/result data arrives via
/// OnEventReceived, subscribed once to the shared IEventStreamService and
/// filtered by CorrelationId (ui-data-contracts.md §10).
/// </summary>
public partial class PipelineExecutionViewModel : ObservableObject
{
    private readonly IPipelineService _pipelineService;

    private string? _activeCorrelationId;
    private PipelineJobKind _activeKind;

    public PipelineExecutionViewModel(IPipelineService pipelineService, IEventStreamService eventStream)
    {
        _pipelineService = pipelineService;
        eventStream.EventReceived += OnEventReceived;
        eventStream.TransportFaulted += OnTransportFaulted;
    }

    public RawAstViewModel RawAstViewModel { get; } = new();

    public NormalizedAstViewModel NormalizedAstViewModel { get; } = new();

    public IrViewModel IrViewModel { get; } = new();

    public PromptViewModel PromptViewModel { get; } = new();

    public ModelOutputViewModel ModelOutputViewModel { get; } = new();

    public FinalResultViewModel FinalResultViewModel { get; } = new();

    /// <summary>
    /// The single canonical execution-state flag (ui-viewmodels.md §6) - true
    /// from the ack until final_result_ready/pipeline_failed (or, for
    /// /explain, until normalized_ast_generated). Every other ViewModel binds
    /// to this directly rather than keeping its own copy.
    /// </summary>
    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _hasErrors;

    public ObservableCollection<UiError> Errors { get; } = [];

    /// <summary>
    /// ui-routing-navigation.md §9: sidebar cannot navigate to Execution
    /// unless a pipeline has produced a result. Set as soon as an execution
    /// is accepted (even one that later fails still has inspector data to show).
    /// </summary>
    public bool HasResult { get; private set; }

    /// <summary>
    /// Clears the global error banner - called by the composition root when
    /// the user navigates away from the Execution Page (only reachable once
    /// IsRunning is false), per ui-viewmodels.md §10 / ui-error-handling.md §8.
    /// </summary>
    public void ClearErrors()
    {
        Errors.Clear();
        HasErrors = false;
    }

    public Task<PipelineCallOutcome> RunPipelineAsync(string source) =>
        StartAsync(PipelineJobKind.Run, _pipelineService.RunAsync, source);

    public Task<PipelineCallOutcome> ExplainPipelineAsync(string source) =>
        StartAsync(PipelineJobKind.Explain, _pipelineService.ExplainAsync, source);

    public Task<PipelineCallOutcome> TracePipelineAsync(string source) =>
        StartAsync(PipelineJobKind.Trace, _pipelineService.TraceAsync, source);

    private async Task<PipelineCallOutcome> StartAsync(
        PipelineJobKind kind,
        Func<string, Task<PipelineStartResult>> start,
        string source)
    {
        // Must resume on the UI thread: Errors/inspector state below are bound
        // to Avalonia controls (ErrorBanner etc.), which throw on cross-thread
        // access - ConfigureAwait(false) here previously dropped the captured
        // UI SynchronizationContext and crashed the app on every Run/Explain/Trace.
        var result = await start(source);

        Errors.Clear();
        if (!result.Accepted)
        {
            foreach (var error in result.Errors)
            {
                Errors.Add(error);
            }

            return PipelineCallOutcome.Blocked;
        }

        ResetInspectors();
        _activeKind = kind;
        _activeCorrelationId = result.CorrelationId;
        IsRunning = true;
        HasErrors = false;
        HasResult = true;
        return PipelineCallOutcome.NavigateToExecution;
    }

    private void ResetInspectors()
    {
        RawAstViewModel.Reset();
        NormalizedAstViewModel.Reset();
        IrViewModel.Reset();
        PromptViewModel.Reset();
        ModelOutputViewModel.Reset();
        FinalResultViewModel.Reset();
    }

    private void OnEventReceived(WsEvent wsEvent)
    {
        if (wsEvent.CorrelationId != _activeCorrelationId)
        {
            // Stale event from a superseded execution, or an event meant for
            // EditorViewModel's independent live-validation stream - ignore
            // (ui-data-contracts.md §10).
            return;
        }

        switch (wsEvent.EventType)
        {
            case "pipeline_started":
                ResetInspectors();
                HasErrors = false;
                Errors.Clear();
                break;

            case "raw_ast_generated":
                ApplyRawAst(Deserialize<RawAstEventData>(wsEvent).RawAst);
                break;

            case "normalized_ast_generated":
                ApplyNormalizedAst(Deserialize<NormalizedAstEventData>(wsEvent).NormalizedAst);
                if (_activeKind == PipelineJobKind.Explain)
                {
                    // /explain never evaluates - this is its terminal event.
                    IsRunning = false;
                }

                break;

            case "ir_generated":
                ApplyIr(Deserialize<IrEventData>(wsEvent).Ir);
                break;

            case "prompts_generated":
                foreach (var prompt in Deserialize<PromptsEventData>(wsEvent).Prompts)
                {
                    PromptViewModel.Prompts.Add(prompt);
                }

                break;

            case "model_outputs_generated":
                foreach (var output in Deserialize<ModelOutputsEventData>(wsEvent).ModelOutputs)
                {
                    ModelOutputViewModel.Outputs.Add(output);
                }

                break;

            case "final_result_ready":
                ApplyFinalResult(Deserialize<RunData>(wsEvent).FinalResult);
                IsRunning = false;
                break;

            case "pipeline_failed":
                HasErrors = true;
                foreach (var error in wsEvent.Errors)
                {
                    Errors.Add(error);
                }

                DistributeErrorsToInspectors(wsEvent.Errors);
                IsRunning = false;
                break;
        }
    }

    private void OnTransportFaulted(UiError error)
    {
        if (!IsRunning)
        {
            return;
        }

        Errors.Add(error);
        HasErrors = true;
        IsRunning = false;
    }

    private static TEventData Deserialize<TEventData>(WsEvent wsEvent) =>
        wsEvent.Data!.Value.Deserialize<TEventData>(PipelineJsonOptions.Default)!;

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

    private void ApplyIr(IrResponse ir)
    {
        IrViewModel.RawText = ir.RawText;
        IrViewModel.Metadata = ir.Metadata;
        foreach (var operation in ir.Operations)
        {
            IrViewModel.Operations.Add(operation);
        }
    }

    private void ApplyFinalResult(FinalResult finalResult)
    {
        FinalResultViewModel.ResultText = finalResult.Text;
        FinalResultViewModel.RawText = finalResult.Text;
        FinalResultViewModel.ContentType = MapContentType(finalResult.ContentType);
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

    private static Inspectors.ResultContentType MapContentType(Services.Dto.ResultContentType wireType) => wireType switch
    {
        Services.Dto.ResultContentType.Markdown => Inspectors.ResultContentType.Markdown,
        Services.Dto.ResultContentType.Json => Inspectors.ResultContentType.Json,
        _ => Inspectors.ResultContentType.PlainText,
    };
}
