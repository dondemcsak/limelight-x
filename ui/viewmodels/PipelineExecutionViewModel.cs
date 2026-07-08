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
/// final_result_ready, since it never invokes the evaluator), while Run
/// (which now invokes /trace, ui-viewmodels.md §6) ends at final_result_ready
/// (api.md §2.1).
/// </summary>
internal enum PipelineJobKind
{
    Run,
    Explain,
}

/// <summary>
/// Coordinates one .llx tab's pipeline execution and inspector ViewModels
/// (ui-viewmodels.md §7) - one instance per CnlTabViewModel, not an app-wide
/// singleton. Triggered by that tab's own EditorViewModel via
/// RunRequested/ExplainRequested, wired directly by CnlTabViewModel's
/// constructor; not bound to directly from any View.
///
/// Run/Explain only await the immediate ack; actual stage/result data
/// arrives via OnEventReceived, subscribed to the shared IEventStreamService
/// and filtered by this tab's own CorrelationId (ui-data-contracts.md §10).
/// </summary>
public partial class PipelineExecutionViewModel : ObservableObject, IDisposable
{
    private readonly IPipelineService _pipelineService;
    private readonly IEventStreamService _eventStream;
    private readonly IExecutionLockService _executionLock;

    private string? _activeCorrelationId;
    private PipelineJobKind _activeKind;

    public PipelineExecutionViewModel(IPipelineService pipelineService, IEventStreamService eventStream, IExecutionLockService executionLock)
    {
        _pipelineService = pipelineService;
        _eventStream = eventStream;
        _executionLock = executionLock;
        eventStream.EventReceived += OnEventReceived;
        eventStream.TransportFaulted += OnTransportFaulted;

        // HasErrors is a computed proxy over ErrorBanner.IsVisible (below) -
        // forward its change notification so existing bindings/subscribers to
        // HasErrors keep working unchanged.
        ErrorBanner.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ErrorBannerViewModel.IsVisible))
            {
                OnPropertyChanged(nameof(HasErrors));
            }
        };
    }

    public string? CorrelationId => _activeCorrelationId;

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

    /// <summary>
    /// True from a `prompt_generated` event until the matching `model_output_generated`
    /// arrives - drives the "Waiting for model response..." LoadingIndicator
    /// (ui-components.md §4.4) shown between PromptPanel and ModelOutputPanel, since
    /// ModelOutputPanel itself stays hidden until its first output arrives
    /// (ui-components.md §5.6) and would otherwise leave that wait with no visible feedback.
    /// </summary>
    [ObservableProperty]
    private bool _isAwaitingModelOutput;

    /// <summary>This tab's error banner (ui-components.md §7.1, ui-viewmodels.md §7) - "populate this tab's error banner" on pipeline_failed means this.</summary>
    public ErrorBannerViewModel ErrorBanner { get; } = new();

    public ObservableCollection<UiError> Errors => ErrorBanner.Errors;

    public bool HasErrors => ErrorBanner.IsVisible;

    /// <summary>
    /// Clears this tab's error banner - called on user dismiss
    /// (ui-error-handling.md §8) or before starting a new execution.
    /// </summary>
    public void ClearErrors() => ErrorBanner.Clear();

    /// <summary>Run now invokes /trace (ui-viewmodels.md §6) - the old bare-/run two-event sequence is no longer reachable from the UI.</summary>
    public Task RunPipelineAsync(string source) =>
        StartAsync(PipelineJobKind.Run, _pipelineService.TraceAsync, source);

    public Task ExplainPipelineAsync(string source) =>
        StartAsync(PipelineJobKind.Explain, _pipelineService.ExplainAsync, source);

    private async Task StartAsync(
        PipelineJobKind kind,
        Func<string, Task<PipelineStartResult>> start,
        string source)
    {
        // Must resume on the UI thread: Errors/inspector state below are bound
        // to Avalonia controls (ErrorBanner etc.), which throw on cross-thread
        // access - ConfigureAwait(false) here previously dropped the captured
        // UI SynchronizationContext and crashed the app on every Run/Explain.
        var result = await start(source);

        ErrorBanner.Clear();
        if (!result.Accepted)
        {
            // Ack-phase failure (e.g. transport unreachable) - nothing to
            // stream, so there's no lock to acquire in the first place.
            ErrorBanner.Show(result.Errors);
            return;
        }

        ResetInspectors();
        _activeKind = kind;
        _activeCorrelationId = result.CorrelationId;
        // IsRunning/lock-acquire happen on pipeline_started (OnEventReceived
        // below), not here - the ack only confirms the request was accepted,
        // not that the server has actually begun streaming yet.
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
                ErrorBanner.Clear();
                IsRunning = true;
                IsAwaitingModelOutput = false;
                _executionLock.TryAcquire(this);
                break;

            case "raw_ast_generated":
                ApplyRawAst(Deserialize<RawAstEventData>(wsEvent).RawAst);
                RawAstViewModel.IsCollapsed = false;
                break;

            case "normalized_ast_generated":
                ApplyNormalizedAst(Deserialize<NormalizedAstEventData>(wsEvent).NormalizedAst);
                NormalizedAstViewModel.IsCollapsed = false;
                if (_activeKind == PipelineJobKind.Explain)
                {
                    // /explain never evaluates - this is its terminal event.
                    IsRunning = false;
                    _executionLock.Release(this);
                }

                break;

            case "ir_generated":
                ApplyIr(Deserialize<IrEventData>(wsEvent).Ir);
                IrViewModel.IsCollapsed = false;
                break;

            case "prompt_generated":
                PromptViewModel.Prompts.Add(Deserialize<PromptEventData>(wsEvent).Prompt);
                if (PromptViewModel.Prompts.Count == 1)
                {
                    // Auto-expand only on the first prompt of this execution -
                    // later prompts append/auto-scroll without re-triggering
                    // the expand transition (ui-viewmodels.md §7).
                    PromptViewModel.IsCollapsed = false;
                }

                IsAwaitingModelOutput = true;
                break;

            case "model_output_generated":
                ModelOutputViewModel.Outputs.Add(Deserialize<ModelOutputEventData>(wsEvent).ModelOutput);
                if (ModelOutputViewModel.Outputs.Count == 1)
                {
                    ModelOutputViewModel.IsCollapsed = false;
                }

                IsAwaitingModelOutput = false;
                break;

            case "final_result_ready":
                ApplyFinalResult(Deserialize<RunData>(wsEvent).FinalResult);
                FinalResultViewModel.IsCollapsed = false;
                IsRunning = false;
                IsAwaitingModelOutput = false;
                _executionLock.Release(this);
                break;

            case "pipeline_failed":
                ErrorBanner.Show(wsEvent.Errors);
                DistributeErrorsToInspectors(wsEvent.Errors);
                IsRunning = false;
                IsAwaitingModelOutput = false;
                _executionLock.Release(this);
                break;
        }
    }

    private void OnTransportFaulted(UiError error)
    {
        if (!IsRunning)
        {
            return;
        }

        ErrorBanner.Show(error);
        IsRunning = false;
        _executionLock.Release(this);
    }

    /// <summary>
    /// Confirmed decision (approved migration plan): if this tab is closed
    /// while its execution is still in flight, the app-wide lock releases
    /// immediately rather than waiting for a terminal event that a disposed
    /// ViewModel should no longer react to.
    /// </summary>
    public void Dispose()
    {
        _eventStream.EventReceived -= OnEventReceived;
        _eventStream.TransportFaulted -= OnTransportFaulted;
        if (IsRunning)
        {
            _executionLock.Release(this);
        }
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
