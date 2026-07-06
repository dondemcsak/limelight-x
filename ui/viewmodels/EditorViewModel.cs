using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
/// Completion/quick-fix/hover (CompletionItems, QuickFixes, HoverInfo,
/// SelectCompletionItemCommand, ApplyQuickFixCommand) are stubbed pending
/// Phase 4b's grammar-driven completion engine - a materially separate,
/// larger piece of work than the rest of this phase (see the approved plan).
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
    private CancellationTokenSource? _validationDebounceCts;
    private string? _validationCorrelationId;

    public EditorViewModel(IPipelineService pipelineService, IEventStreamService eventStream, IExecutionLockService executionLock)
    {
        _pipelineService = pipelineService;
        _eventStream = eventStream;
        _executionLock = executionLock;
        eventStream.EventReceived += OnValidationEventReceived;
        _executionLock.ExecutionLockChanged += NotifyPipelineCommandsCanExecuteChanged;
    }

    /// <summary>Unsubscribes from the shared event stream/lock so a closed tab's EditorViewModel no longer reacts to anything (ui-viewmodels.md §5.2: disposed alongside its owning CnlTabViewModel).</summary>
    public void Dispose()
    {
        _validationDebounceCts?.Cancel();
        _eventStream.EventReceived -= OnValidationEventReceived;
        _executionLock.ExecutionLockChanged -= NotifyPipelineCommandsCanExecuteChanged;
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

    [RelayCommand]
    private void SelectCompletionItem(CompletionItem item)
    {
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
