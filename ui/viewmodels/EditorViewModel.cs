using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LimelightX.UI.Intellisense;
using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Editor;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels;

/// <summary>
/// Primary ViewModel for CNL editing (ui-viewmodels.md §6), one instance per
/// open .llx tab (owned by CnlTabViewModel). Live validation calls
/// PipelineService.ExplainAsync on a debounce timer and subscribes to its own
/// (independent) slice of the shared event stream, filtered by its own
/// CorrelationId - it never touches this tab's PipelineExecutionViewModel
/// state, and is exempt from IExecutionLockService (ui-viewmodels.md §6 Live
/// Validation).
/// Completion/hover/folding/local-diagnostics (CompletionItems, HoverInfo,
/// FoldRegions, LocalDiagnostics and their Request*/Refresh* triggers below)
/// are wired to the IParserHost/ICompletionService/IDiagnosticService/
/// IHoverService/IFoldingService interfaces (ui/intellisense/), but those
/// implementations are still stubs throwing NotImplementedException - see
/// the BDD-tests-first implementation plan. Deliberately NOT wired to
/// OnTextChanged yet (unlike the eventual real behavior described in
/// ui-viewmodels.md §6 "IntelliSense (Tree-sitter)"): every trigger below is
/// explicit-call-only for now, so existing text-driven tests are unaffected
/// by services that aren't implemented yet.
/// QuickFixes/ApplyQuickFixCommand stay empty - DiagnosticService produces
/// advisory diagnostics only, not actionable fixes (ui-viewmodels.md §6).
/// FormatCommand is stubbed: no formatting-rule spec content exists anywhere
/// in spec/ux/*, so it cannot be implemented yet (flagged ambiguity).
/// </summary>
public partial class EditorViewModel : ObservableObject, IDisposable
{
    // Live-validation debounce: not specified anywhere in spec/ux/*
    // (flagged ambiguity) - 450ms is a reasonable editor-validation debounce,
    // tunable here if it proves wrong in practice.
    private static readonly TimeSpan ValidationDebounce = TimeSpan.FromMilliseconds(450);

    private readonly IPipelineService _pipelineService;
    private readonly IEventStreamService _eventStream;
    private readonly IExecutionLockService _executionLock;
    private readonly IParserHost _parserHost;
    private readonly ICompletionService _completionService;
    private readonly IDiagnosticService _diagnosticService;
    private readonly IHoverService _hoverService;
    private readonly IFoldingService _foldingService;
    private readonly IStructuralSelectionService _structuralSelectionService;
    private CancellationTokenSource? _validationDebounceCts;
    private string? _validationCorrelationId;

    public EditorViewModel(
        IPipelineService pipelineService,
        IEventStreamService eventStream,
        IExecutionLockService executionLock,
        IParserHost parserHost,
        ICompletionService completionService,
        IDiagnosticService diagnosticService,
        IHoverService hoverService,
        IFoldingService foldingService,
        IStructuralSelectionService structuralSelectionService)
    {
        _pipelineService = pipelineService;
        _eventStream = eventStream;
        _executionLock = executionLock;
        _parserHost = parserHost;
        _completionService = completionService;
        _diagnosticService = diagnosticService;
        _hoverService = hoverService;
        _foldingService = foldingService;
        _structuralSelectionService = structuralSelectionService;
        eventStream.EventReceived += OnValidationEventReceived;
        _executionLock.ExecutionLockChanged += NotifyPipelineCommandsCanExecuteChanged;
    }

    /// <summary>Unsubscribes from the shared event stream/lock, and disposes this tab's IParserHost, so a closed tab's EditorViewModel no longer reacts to anything (ui-viewmodels.md §5.2: disposed alongside its owning CnlTabViewModel).</summary>
    public void Dispose()
    {
        _validationDebounceCts?.Cancel();
        _eventStream.EventReceived -= OnValidationEventReceived;
        _executionLock.ExecutionLockChanged -= NotifyPipelineCommandsCanExecuteChanged;
        _parserHost.Dispose();
    }

    [ObservableProperty]
    private string _text = string.Empty;

    partial void OnTextChanged(string value)
    {
        QueueValidation();
        NotifyPipelineCommandsCanExecuteChanged();
    }

    [ObservableProperty]
    private int _cursorPosition;

    [ObservableProperty]
    private (int Start, int End) _selectionRange;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private bool _isFormatting;

    [ObservableProperty]
    private bool _isCompleting;

    [ObservableProperty]
    private HoverInfo? _hoverInfo;

    public ObservableCollection<ValidationError> ValidationErrors { get; } = [];

    public ObservableCollection<CompletionItem> CompletionItems { get; } = [];

    public ObservableCollection<QuickFixItem> QuickFixes { get; } = [];

    /// <summary>Client-side folding regions, one per CNL sentence (bdd-ui-interactions.md §2.9). Populated by RefreshDecorations.</summary>
    public ObservableCollection<FoldRegion> FoldRegions { get; } = [];

    /// <summary>Advisory, local-only diagnostics from Tree-sitter ERROR/MISSING nodes (bdd-ui-interactions.md §2.7-§2.8) - never authoritative, never written into SyntaxErrors/ValidationErrors. Populated by RefreshDecorations.</summary>
    public ObservableCollection<LocalDiagnostic> LocalDiagnostics { get; } = [];

    /// <summary>Error-or-higher validation errors promoted to this tab's banner (ui-error-handling.md §9) - the ErrorBanner component binds directly to this.</summary>
    public ErrorBannerViewModel ErrorBanner { get; } = new();

    public ObservableCollection<UiError> Errors => ErrorBanner.Errors;

    // See EditorAction's doc comment: these stay empty by design. Real
    // undo/redo state lives in AvaloniaEdit's TextDocument, reached via the
    // Undo/RedoRequested bridge below.
    public Stack<EditorAction> UndoStack { get; } = new();

    public Stack<EditorAction> RedoStack { get; } = new();

    /// <summary>CnlEditor subscribes and forwards to its TextEditor.Undo().</summary>
    public event Action? UndoRequested;

    /// <summary>CnlEditor subscribes and forwards to its TextEditor.Redo().</summary>
    public event Action? RedoRequested;

    [RelayCommand]
    private void Undo() => UndoRequested?.Invoke();

    [RelayCommand]
    private void Redo() => RedoRequested?.Invoke();

    [RelayCommand]
    private Task FormatAsync() => Task.CompletedTask;

    [RelayCommand]
    private void ApplyQuickFix(QuickFixItem item)
    {
    }

    /// <summary>
    /// Intentionally empty: the real completion window (CnlEditor + AvaloniaEdit's
    /// CompletionWindow/CnlCompletionData) applies the selected item's text
    /// directly via AvaloniaEdit's own document API, not through this
    /// command - see CnlCompletionData.Complete(). Kept as documented API
    /// surface (ui-viewmodels.md §6) for any future non-AvaloniaEdit caller.
    /// </summary>
    [RelayCommand]
    private void SelectCompletionItem(CompletionItem item)
    {
    }

    /// <summary>
    /// Reparses Text and recomputes FoldRegions/LocalDiagnostics
    /// (bdd-ui-interactions.md §2.7-§2.9). Explicit-call-only for now - see
    /// this class's doc comment.
    /// </summary>
    public void RefreshDecorations()
    {
        var root = _parserHost.Parse(Text);

        LocalDiagnostics.Clear();
        foreach (var diagnostic in _diagnosticService.GetDiagnostics(root))
        {
            LocalDiagnostics.Add(diagnostic);
        }

        FoldRegions.Clear();
        foreach (var fold in _foldingService.GetFolds(root))
        {
            FoldRegions.Add(fold);
        }
    }

    /// <summary>Completion trigger (bdd-ui-interactions.md §2.12-§2.13) - explicit, not on every keystroke.</summary>
    public void RequestCompletionsAt(int cursorByte)
    {
        var root = _parserHost.Parse(Text);

        CompletionItems.Clear();
        foreach (var item in _completionService.GetCompletions(Text, root, cursorByte))
        {
            CompletionItems.Add(item);
        }
    }

    /// <summary>Hover trigger, pointer-driven not caret-driven (bdd-ui-interactions.md §2.11) - CnlEditor calls this from Editor.PointerHover.</summary>
    public void RequestHoverAt(int cursorByte) => HoverInfo = _hoverService.GetHover(Text, _parserHost.Parse(Text), cursorByte);

    /// <summary>CnlEditor calls this from Editor.PointerHoverStopped.</summary>
    public void ClearHover() => HoverInfo = null;

    /// <summary>
    /// Structural selection (bdd-ui-interactions.md §2.10, cnl-editor-architecture.md
    /// §1 "structural selection"): grows SelectionRange to the smallest
    /// strictly-larger enclosing CST node on each invocation. SelectionRange
    /// is UTF-16 char offsets (matches CnlEditor's SelectionStart/Length
    /// binding); IStructuralSelectionService operates in UTF-8 byte offsets
    /// like every other Tree-sitter-backed service, so this converts both ways.
    /// </summary>
    public void ExpandSelection()
    {
        var utf8Text = new Utf8Text(Text);
        var startByte = utf8Text.CharOffsetToByteOffset(SelectionRange.Start);
        var endByte = utf8Text.CharOffsetToByteOffset(SelectionRange.End);

        var root = _parserHost.Parse(Text);
        var (newStartByte, newEndByte) = _structuralSelectionService.ExpandSelection(root, startByte, endByte);

        SelectionRange = (utf8Text.ByteOffsetToCharOffset(newStartByte), utf8Text.ByteOffsetToCharOffset(newEndByte));
    }

    /// <summary>
    /// Wired by the owning CnlTabViewModel to this tab's own
    /// PipelineExecutionViewModel.RunPipelineAsync/ExplainPipelineAsync. Plain
    /// Func delegates rather than a direct reference, keeping EditorViewModel
    /// free of a compile-time dependency on PipelineExecutionViewModel.
    /// </summary>
    public Func<string, Task>? RunRequested { get; set; }

    public Func<string, Task>? ExplainRequested { get; set; }

    [RelayCommand(CanExecute = nameof(CanExecutePipelineCommand))]
    private Task RunAsync() => RunRequested?.Invoke(Text) ?? Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanExecutePipelineCommand))]
    private Task ExplainAsync() => ExplainRequested?.Invoke(Text) ?? Task.CompletedTask;

    /// <summary>
    /// ui-viewmodels.md §6: Run/Explain are blocked only while any tab's
    /// execution is in flight app-wide, or the editor is empty. Unlike the
    /// old page-based model, a known validation error no longer blocks these
    /// commands client-side - the backend's own pipeline_failed/ERR_CNL_PARSE
    /// response is now the sole gate for invalid CNL (confirmed decision,
    /// see the approved migration plan).
    /// </summary>
    private bool CanExecutePipelineCommand() => !_executionLock.IsAnyExecutionRunning && !string.IsNullOrWhiteSpace(Text);

    /// <summary>Called whenever IExecutionLockService.IsAnyExecutionRunning changes, or this tab's own text changes.</summary>
    public void NotifyPipelineCommandsCanExecuteChanged()
    {
        RunCommand.NotifyCanExecuteChanged();
        ExplainCommand.NotifyCanExecuteChanged();
    }

    private void QueueValidation()
    {
        _validationDebounceCts?.Cancel();
        _validationCorrelationId = null;

        if (string.IsNullOrWhiteSpace(Text))
        {
            ValidationErrors.Clear();
            ErrorBanner.Clear();
            return;
        }

        var cts = new CancellationTokenSource();
        _validationDebounceCts = cts;
        _ = ValidateAfterDelayAsync(cts.Token);
    }

    private async Task ValidateAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ValidationDebounce, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        IsValidating = true;
        var result = await _pipelineService.ExplainAsync(Text);
        if (cancellationToken.IsCancellationRequested)
        {
            IsValidating = false;
            return;
        }

        ValidationErrors.Clear();
        ErrorBanner.Clear();

        if (!result.Accepted)
        {
            ApplyValidationErrors(result.Errors);
            IsValidating = false;
            return;
        }

        // Stage/failure data arrives via OnValidationEventReceived, filtered
        // by this correlation_id; IsValidating clears there.
        _validationCorrelationId = result.CorrelationId;
    }

    private void OnValidationEventReceived(WsEvent wsEvent)
    {
        if (wsEvent.CorrelationId != _validationCorrelationId)
        {
            return;
        }

        switch (wsEvent.EventType)
        {
            case "pipeline_started":
                ValidationErrors.Clear();
                Errors.Clear();
                break;

            case "normalized_ast_generated":
                // /explain's terminal event on success - nothing further to apply.
                IsValidating = false;
                _validationCorrelationId = null;
                break;

            case "pipeline_failed":
                ApplyValidationErrors(wsEvent.Errors);
                IsValidating = false;
                _validationCorrelationId = null;
                break;
        }
    }

    /// <summary>
    /// ui-error-handling.md §6.2: parser/grammar/hole are all ERR_CNL_PARSE
    /// at the wire level - classification into a display kind happens purely
    /// client-side (CnlErrorClassifier), from Text and each error's Location.
    /// </summary>
    private void ApplyValidationErrors(IReadOnlyList<UiError> errors)
    {
        foreach (var error in errors)
        {
            var kind = error.Code == "ERR_CNL_PARSE"
                ? CnlErrorClassifier.Classify(Text, error.Location, error.Message)
                : CnlErrorKind.Parser;

            var validationError = new ValidationError
            {
                Code = error.Code,
                Message = error.Message,
                Severity = error.Severity,
                Category = error.Category,
                Location = error.Location,
                Kind = kind,
            };

            ValidationErrors.Add(validationError);

            // ui-error-handling.md §9: validation errors also show a global
            // banner when severity is error-or-higher (info/warning stay inline-only).
            if (error.Severity is ErrorSeverity.Error or ErrorSeverity.Fatal)
            {
                ErrorBanner.Show(validationError);
            }
        }
    }
}
