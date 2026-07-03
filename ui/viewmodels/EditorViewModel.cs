using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LimelightX.UI.Services;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels;

/// <summary>
/// Primary ViewModel for CNL editing (ui-viewmodels.md §4.1). Live validation
/// calls PipelineService.ExplainAsync on a debounce timer; Run/Explain/Trace
/// are stubbed here and get their real implementation in Phase 5
/// (PipelineExecutionViewModel navigates to ExecutionPage on success).
/// Completion/quick-fix/hover (CompletionItems, QuickFixes, HoverInfo,
/// SelectCompletionItemCommand, ApplyQuickFixCommand) are stubbed pending
/// Phase 4b's grammar-driven completion engine - a materially separate,
/// larger piece of work than the rest of this phase (see the approved plan).
/// FormatCommand is stubbed: no formatting-rule spec content exists anywhere
/// in spec/ux/*, so it cannot be implemented yet (flagged ambiguity).
/// </summary>
public partial class EditorViewModel : ObservableObject
{
    // Live-validation debounce: not specified anywhere in spec/ux/*
    // (flagged ambiguity) - 450ms is a reasonable editor-validation debounce,
    // tunable here if it proves wrong in practice.
    private static readonly TimeSpan ValidationDebounce = TimeSpan.FromMilliseconds(450);

    private readonly IPipelineService _pipelineService;
    private CancellationTokenSource? _validationDebounceCts;

    public EditorViewModel(IPipelineService pipelineService)
    {
        _pipelineService = pipelineService;
        ValidationErrors.CollectionChanged += (_, _) => NotifyPipelineCommandsCanExecuteChanged();
    }

    [ObservableProperty]
    private string _text = string.Empty;

    partial void OnTextChanged(string value) => QueueValidation();

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

    public ObservableCollection<UiError> Errors { get; } = [];

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
    /// Set by the composition root once PipelineExecutionViewModel exists
    /// (Phase 5): calls the corresponding PipelineExecutionViewModel method
    /// with Text and navigates to ExecutionPage on a non-Blocked outcome.
    /// Plain Func delegates (not events) since the caller must await the
    /// full call+navigate sequence, matching NavigationViewModel's existing
    /// guard-delegate pattern.
    /// </summary>
    public Func<string, Task>? RunRequested { get; set; }

    public Func<string, Task>? ExplainRequested { get; set; }

    public Func<string, Task>? TraceRequested { get; set; }

    [RelayCommand(CanExecute = nameof(CanExecutePipelineCommand))]
    private Task RunAsync() => RunRequested?.Invoke(Text) ?? Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanExecutePipelineCommand))]
    private Task ExplainAsync() => ExplainRequested?.Invoke(Text) ?? Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanExecutePipelineCommand))]
    private Task TraceAsync() => TraceRequested?.Invoke(Text) ?? Task.CompletedTask;

    /// <summary>Guard 2 (ui-routing-navigation.md §4): validation errors block Run/Explain/Trace.</summary>
    private bool CanExecutePipelineCommand() => ValidationErrors.Count == 0;

    private void NotifyPipelineCommandsCanExecuteChanged()
    {
        RunCommand.NotifyCanExecuteChanged();
        ExplainCommand.NotifyCanExecuteChanged();
        TraceCommand.NotifyCanExecuteChanged();
    }

    private void QueueValidation()
    {
        _validationDebounceCts?.Cancel();

        if (string.IsNullOrWhiteSpace(Text))
        {
            ValidationErrors.Clear();
            Errors.Clear();
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
        try
        {
            var result = await _pipelineService.ExplainAsync(Text);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ValidationErrors.Clear();
            Errors.Clear();
            foreach (var error in result.Errors)
            {
                var validationError = new ValidationError
                {
                    Code = error.Code,
                    Message = error.Message,
                    Severity = error.Severity,
                    Category = error.Category,
                    Location = error.Location,
                };

                ValidationErrors.Add(validationError);

                // ui-error-handling.md §9: validation errors also show a global
                // banner when severity is error-or-higher (info/warning stay inline-only).
                if (error.Severity is ErrorSeverity.Error or ErrorSeverity.Fatal)
                {
                    Errors.Add(validationError);
                }
            }
        }
        finally
        {
            IsValidating = false;
        }
    }
}
