using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LimelightX.UI.Intellisense;
using LimelightX.UI.Services;

namespace LimelightX.UI.ViewModels;

/// <summary>
/// Primary ViewModel for CNL editing (ui-viewmodels.md §6), one instance per
/// open .llx tab (owned by CnlTabViewModel). The editor never calls
/// /src/api on its own - the backend is reached only via RunRequested/
/// ExplainRequested, wired by CnlTabViewModel to this tab's own
/// PipelineExecutionViewModel, itself only invoked by an explicit Run/Explain
/// click (cnl-editor-architecture.md §5). There used to be a separate
/// "Live Validation" mechanism here that called /explain on a debounce timer
/// after every keystroke - removed, since real-time syntax feedback now
/// comes entirely from Tree-sitter's local LocalDiagnostics (squiggle+hover,
/// bdd-ui-interactions.md §2.16-§2.17), and backend-authoritative errors
/// already have a home in PipelineExecutionViewModel.ErrorBanner, shown only
/// when the user actually clicks Run or Explain.
/// Completion/hover/folding/outline/local-diagnostics (CompletionItems,
/// HoverInfo, FoldRegions, Outline, LocalDiagnostics and their Request*/
/// Refresh* triggers below) are wired to the real, Tree-sitter-backed
/// IParserHost/ICompletionService/IDiagnosticService/IHoverService/
/// IFoldingService/IOutlineService implementations (ui/intellisense/).
/// RefreshDecorations() runs synchronously from OnTextChanged
/// (bdd-ui-interactions.md §2.7a, ui-viewmodels.md §6), so LocalDiagnostics/
/// QuickFixes/FoldRegions/Outline are never stale relative to Text.
/// QuickFixes is rebuilt from LocalDiagnostics entries carrying a
/// SuggestedFix; GhostSuggestion tracks whichever QuickFixes entry sits at
/// the current CursorPosition, and ApplyQuickFixCommand commits it into Text
/// (bdd-ui-interactions.md §2.18-§2.19).
/// FormatCommand is stubbed: no formatting-rule spec content exists anywhere
/// in spec/ux/*, so it cannot be implemented yet (flagged ambiguity).
/// </summary>
public partial class EditorViewModel : ObservableObject, IDisposable
{
    private readonly IExecutionLockService _executionLock;
    private readonly IParserHost _parserHost;
    private readonly ICompletionService _completionService;
    private readonly IDiagnosticService _diagnosticService;
    private readonly IHoverService _hoverService;
    private readonly IFoldingService _foldingService;
    private readonly IStructuralSelectionService _structuralSelectionService;
    private readonly IOutlineService _outlineService;
    private readonly IAutoPairService _autoPairService;
    private readonly INavigationService _navigationService;

    public EditorViewModel(
        IExecutionLockService executionLock,
        IParserHost parserHost,
        ICompletionService completionService,
        IDiagnosticService diagnosticService,
        IHoverService hoverService,
        IFoldingService foldingService,
        IStructuralSelectionService structuralSelectionService,
        IOutlineService outlineService,
        IAutoPairService autoPairService,
        INavigationService navigationService)
    {
        _executionLock = executionLock;
        _parserHost = parserHost;
        _completionService = completionService;
        _diagnosticService = diagnosticService;
        _hoverService = hoverService;
        _foldingService = foldingService;
        _structuralSelectionService = structuralSelectionService;
        _outlineService = outlineService;
        _autoPairService = autoPairService;
        _navigationService = navigationService;
        _executionLock.ExecutionLockChanged += NotifyPipelineCommandsCanExecuteChanged;
    }

    /// <summary>Unsubscribes from the execution lock, and disposes this tab's IParserHost, so a closed tab's EditorViewModel no longer reacts to anything (ui-viewmodels.md §5.2: disposed alongside its owning CnlTabViewModel).</summary>
    public void Dispose()
    {
        _executionLock.ExecutionLockChanged -= NotifyPipelineCommandsCanExecuteChanged;
        _parserHost.Dispose();
    }

    [ObservableProperty]
    private string _text = string.Empty;

    partial void OnTextChanged(string value)
    {
        RefreshDecorations();
        NotifyPipelineCommandsCanExecuteChanged();
    }

    [ObservableProperty]
    private int _cursorPosition;

    partial void OnCursorPositionChanged(int value) => UpdateGhostSuggestion();

    [ObservableProperty]
    private (int Start, int End) _selectionRange;

    [ObservableProperty]
    private bool _isFormatting;

    [ObservableProperty]
    private bool _isCompleting;

    [ObservableProperty]
    private HoverInfo? _hoverInfo;

    /// <summary>Active ghost-text suggestion at the caret, if any (bdd-ui-interactions.md §2.18) - the QuickFixes entry whose InsertionByte equals the current CursorPosition. Committed to real text by Tab via ApplyQuickFixCommand (§2.19).</summary>
    [ObservableProperty]
    private QuickFixItem? _ghostSuggestion;

    public ObservableCollection<CompletionItem> CompletionItems { get; } = [];

    public ObservableCollection<QuickFixItem> QuickFixes { get; } = [];

    /// <summary>Client-side folding regions, one per CNL sentence (bdd-ui-interactions.md §2.9). Populated by RefreshDecorations.</summary>
    public ObservableCollection<FoldRegion> FoldRegions { get; } = [];

    /// <summary>Advisory, local-only diagnostics from Tree-sitter ERROR/MISSING nodes (bdd-ui-interactions.md §2.7-§2.8) - the sole real-time syntax-error surface in the editor; never authoritative. Populated by RefreshDecorations.</summary>
    public ObservableCollection<LocalDiagnostic> LocalDiagnostics { get; } = [];

    /// <summary>Client-side outline entries, one per CNL sentence (ui-intellisense-engine-spec.md §2.5, §10). Populated by RefreshDecorations.</summary>
    public ObservableCollection<OutlineItem> Outline { get; } = [];

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

    /// <summary>Splices item.InsertText into Text at item.InsertionByte, moves the caret past it, and clears GhostSuggestion (bdd-ui-interactions.md §2.19). Invoked by Tab when GhostSuggestion is active, or by any future explicit quick-fix UI.</summary>
    [RelayCommand]
    private void ApplyQuickFix(QuickFixItem item)
    {
        var utf8Text = new Utf8Text(Text);
        var charOffset = utf8Text.ByteOffsetToCharOffset(item.InsertionByte);
        Text = Text[..charOffset] + item.InsertText + Text[charOffset..];
        CursorPosition = charOffset + item.InsertText.Length;
        GhostSuggestion = null;
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
    /// Reparses Text and recomputes FoldRegions/LocalDiagnostics/QuickFixes/
    /// Outline (bdd-ui-interactions.md §2.7-§2.9, §2.18). Runs synchronously
    /// from OnTextChanged (§2.7a); also callable explicitly (e.g. by tests).
    /// </summary>
    public void RefreshDecorations()
    {
        var root = _parserHost.Parse(Text);

        LocalDiagnostics.Clear();
        foreach (var diagnostic in _diagnosticService.GetDiagnostics(root))
        {
            LocalDiagnostics.Add(diagnostic);
        }

        QuickFixes.Clear();
        foreach (var diagnostic in LocalDiagnostics)
        {
            if (diagnostic.SuggestedFix is { } fix)
            {
                QuickFixes.Add(new QuickFixItem { Title = $"Insert '{fix}'", InsertionByte = diagnostic.StartByte, InsertText = fix });
            }
        }

        FoldRegions.Clear();
        foreach (var fold in _foldingService.GetFolds(root))
        {
            FoldRegions.Add(fold);
        }

        Outline.Clear();
        foreach (var item in _outlineService.GetOutline(Text, root))
        {
            Outline.Add(item);
        }

        UpdateGhostSuggestion();
    }

    /// <summary>Sets GhostSuggestion to whichever QuickFixes entry sits at the current CursorPosition, or null (bdd-ui-interactions.md §2.18). Called on every cursor move and after every RefreshDecorations().</summary>
    private void UpdateGhostSuggestion()
    {
        var cursorByte = new Utf8Text(Text).CharOffsetToByteOffset(CursorPosition);
        GhostSuggestion = QuickFixes.FirstOrDefault(fix => fix.InsertionByte == cursorByte);
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

    /// <summary>
    /// Hover trigger, pointer-driven not caret-driven (bdd-ui-interactions.md
    /// §2.11) - CnlEditor calls this from Editor.PointerHover. Checks
    /// LocalDiagnostics first (inclusive span, so a zero-width MISSING span
    /// is still hoverable) and shows the diagnostic's message, taking
    /// priority over grammar-role hover for the same position (§2.17,
    /// ui-intellisense-engine-spec.md §7.5).
    /// </summary>
    public void RequestHoverAt(int cursorByte)
    {
        var diagnostic = LocalDiagnostics.FirstOrDefault(d => cursorByte >= d.StartByte && cursorByte <= d.EndByte);
        HoverInfo = diagnostic != default
            ? new HoverInfo { Text = diagnostic.Message, Position = diagnostic.StartByte }
            : _hoverService.GetHover(Text, _parserHost.Parse(Text), cursorByte);
    }

    /// <summary>CnlEditor calls this from Editor.PointerHoverStopped.</summary>
    public void ClearHover() => HoverInfo = null;

    /// <summary>
    /// Auto-closing pairs (bdd-ui-interactions.md §2.24-§2.25): CnlEditor
    /// calls this right after the opener (`"` or the second `{` of `{{`) is
    /// typed, to decide whether to auto-insert the matching closer.
    /// </summary>
    public bool CanAutoClose(int cursorByte, string opener) =>
        _autoPairService.CanAutoClose(Text, _parserHost.Parse(Text), cursorByte, opener);

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
    /// Go to Definition (bdd-ui-interactions.md §2.26): moves SelectionRange
    /// to the bind_stmt that bound the reference at the current
    /// CursorPosition, if any - a no-op when there is no such binding
    /// (INavigationService.FindDefinition returns null).
    /// </summary>
    public void GoToDefinition()
    {
        var utf8Text = new Utf8Text(Text);
        var cursorByte = utf8Text.CharOffsetToByteOffset(CursorPosition);
        var root = _parserHost.Parse(Text);

        if (_navigationService.FindDefinition(Text, root, cursorByte) is not { } span)
        {
            return;
        }

        SelectionRange = (utf8Text.ByteOffsetToCharOffset(span.Start), utf8Text.ByteOffsetToCharOffset(span.End));
    }

    /// <summary>
    /// Wired by the owning CnlTabViewModel to this tab's own
    /// PipelineExecutionViewModel.RunPipelineAsync/ExplainPipelineAsync. Plain
    /// Func delegates rather than a direct reference, keeping EditorViewModel
    /// free of a compile-time dependency on PipelineExecutionViewModel. This
    /// is the only path by which anything reachable from EditorViewModel ever
    /// calls the backend - only ever invoked by an explicit Run/Explain click.
    /// </summary>
    public Func<string, Task>? RunRequested { get; set; }

    public Func<string, Task>? ExplainRequested { get; set; }

    [RelayCommand(CanExecute = nameof(CanExecutePipelineCommand))]
    private Task RunAsync() => RunRequested?.Invoke(Text) ?? Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanExecutePipelineCommand))]
    private Task ExplainAsync() => ExplainRequested?.Invoke(Text) ?? Task.CompletedTask;

    /// <summary>
    /// ui-viewmodels.md §6: Run/Explain are blocked only while any tab's
    /// execution is in flight app-wide, or the editor is empty. A known
    /// validation error never blocks these commands client-side - the
    /// backend's own pipeline_failed/ERR_CNL_PARSE response, surfaced via
    /// PipelineExecutionViewModel.ErrorBanner once Run/Explain is actually
    /// clicked, is the sole gate for invalid CNL.
    /// </summary>
    private bool CanExecutePipelineCommand() => !_executionLock.IsAnyExecutionRunning && !string.IsNullOrWhiteSpace(Text);

    /// <summary>Called whenever IExecutionLockService.IsAnyExecutionRunning changes, or this tab's own text changes.</summary>
    public void NotifyPipelineCommandsCanExecuteChanged()
    {
        RunCommand.NotifyCanExecuteChanged();
        ExplainCommand.NotifyCanExecuteChanged();
    }
}
