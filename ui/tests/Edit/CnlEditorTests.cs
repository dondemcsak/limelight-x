using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using LimelightX.UI.Components;
using LimelightX.UI.Intellisense;
using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.Tests.TestDoubles;
using LimelightX.UI.ViewModels;
using LimelightX.UI.ViewModels.Tabs;
using Xunit;

namespace LimelightX.UI.Tests.Edit;

/// <summary>
/// Regression coverage for a real bug found during Phase 4 visual
/// verification: CnlEditor's Text bound correctly end-to-end in headless
/// tests, yet rendered as completely blank in the real windowed app,
/// because AvaloniaEdit's control theme
/// (avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml) was never merged
/// into App.axaml - without it, TextEditor has no default template at all.
/// Headless tests don't apply real templates/rendering by default, so this
/// specific failure mode wasn't visible in tests alone; the
/// HasTemplateApplied assertion below at least confirms a template exists.
/// Hosts a bare CnlEditor directly (rather than a full tab/page) since this
/// suite exercises the component itself, not its owning view.
/// </summary>
public class CnlEditorTests
{
    [AvaloniaFact]
    public void SettingText_BeforeAttach_PropagatesToAvaloniaEditOnceAttached()
    {
        var cnlEditor = new CnlEditor { Text = "Load the article from \"a.txt\".\nSummarize it." };

        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(cnlEditor);
        Assert.Equal(cnlEditor.Text, innerEditor.Text);
    }

    [AvaloniaFact]
    public void SettingText_AfterAttach_PropagatesToAvaloniaEditImmediately()
    {
        var cnlEditor = new CnlEditor();
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        cnlEditor.Text = "Load the article from \"a.txt\".\nSummarize it.";
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(cnlEditor);
        Assert.Equal(cnlEditor.Text, innerEditor.Text);
    }

    [AvaloniaFact]
    public void TextEditor_HasTemplateApplied()
    {
        var cnlEditor = new CnlEditor();
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(cnlEditor);

        // A missing control theme (the Phase 4 bug) leaves the TextArea with no
        // visual children at all - this would have caught it directly.
        Assert.NotNull(innerEditor.TextArea);
        Assert.True(innerEditor.GetVisualDescendants().Any(), "TextEditor has no visual children - its control theme is likely not merged into App.axaml.");
    }

    /// <summary>bdd-ui-interactions.md §2.19: Tab commits the active ghost-text suggestion into EditorViewModel.Text and clears GhostSuggestion.</summary>
    [AvaloniaFact]
    public void TabKeyDown_GhostSuggestionActive_CommitsTextAndClearsGhostSuggestion()
    {
        var diagnosticService = new FakeDiagnosticService
        {
            DiagnosticsToReturn = [new LocalDiagnostic("Missing period at end of sentence.", 12, 12, ".")],
        };
        var tab = CreateTab(diagnosticService: diagnosticService);
        tab.Editor.Text = "Summarize it";
        tab.Editor.CursorPosition = 12;
        Assert.NotNull(tab.Editor.GhostSuggestion);

        var cnlEditor = new CnlEditor { DataContext = tab };
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(cnlEditor);
        innerEditor.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(innerEditor.TextArea.IsFocused, "TextArea did not receive focus - KeyPress below would be a no-op.");

        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, string.Empty);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Summarize it.", tab.Editor.Text);
        Assert.Null(tab.Editor.GhostSuggestion);
    }

    /// <summary>bdd-ui-interactions.md §2.19: with no active ghost suggestion, Tab does not commit anything - it falls through to AvaloniaEdit's own default handling (ui-accessibility.md §2).</summary>
    [AvaloniaFact]
    public void TabKeyDown_NoGhostSuggestion_DoesNotCommitAnyText()
    {
        var tab = CreateTab();
        tab.Editor.Text = "Summarize it";

        var cnlEditor = new CnlEditor { DataContext = tab };
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        GetInnerTextEditor(cnlEditor).TextArea.Focus();
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, string.Empty);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Summarize it", tab.Editor.Text);
    }

    /// <summary>bdd-ui-interactions.md §2.24: typing an opening quote at a grammar-valid position auto-inserts the matching closer, caret left between the pair.</summary>
    [AvaloniaFact]
    public void QuoteTyped_AutoPairServiceApproves_InsertsClosingQuoteWithCaretBetween()
    {
        var tab = CreateTab(autoPairService: new FakeAutoPairService { ResultToReturn = true });
        const string before = "Load the article from ";

        var cnlEditor = new CnlEditor { DataContext = tab, Text = before };
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(cnlEditor);
        innerEditor.TextArea.Focus();
        innerEditor.CaretOffset = before.Length;
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("\"");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before + "\"\"", innerEditor.Text);
        Assert.Equal(before.Length + 1, innerEditor.CaretOffset);
    }

    /// <summary>bdd-ui-interactions.md §2.24: typing a quote when the next character is already a quote types through it instead of inserting a duplicate.</summary>
    [AvaloniaFact]
    public void QuoteTyped_NextCharIsAlreadyQuote_TypesThroughInsteadOfDuplicating()
    {
        var tab = CreateTab();
        const string before = "Load the article from \"";
        const string full = before + "\"";

        var cnlEditor = new CnlEditor { DataContext = tab, Text = full };
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(cnlEditor);
        innerEditor.TextArea.Focus();
        innerEditor.CaretOffset = before.Length;
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("\"");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(full, innerEditor.Text);
        Assert.Equal(full.Length, innerEditor.CaretOffset);
    }

    /// <summary>bdd-ui-interactions.md §2.25: typing the second `{` of `{{` at a grammar-valid position auto-inserts `}}`, caret left between.</summary>
    [AvaloniaFact]
    public void DoubleBraceTyped_AutoPairServiceApproves_InsertsClosingBraces()
    {
        var tab = CreateTab(autoPairService: new FakeAutoPairService { ResultToReturn = true });
        const string before = "Summarize it using ";

        var cnlEditor = new CnlEditor { DataContext = tab, Text = before };
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(cnlEditor);
        innerEditor.TextArea.Focus();
        innerEditor.CaretOffset = before.Length;
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("{");
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("{");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before + "{{}}", innerEditor.Text);
        Assert.Equal(before.Length + 2, innerEditor.CaretOffset);
    }

    /// <summary>
    /// bdd-ui-interactions.md §2.34: accepting a completion whose PrefixLength
    /// covers an already-typed partial word replaces exactly that prefix,
    /// never duplicating it (the "SummarizSummarize" bug). Calls
    /// CnlCompletionData.Complete() directly with a zero-length segment at
    /// the caret - exactly the shape AvaloniaEdit's real CompletionWindow
    /// hands it on commit - rather than driving the popup window's own
    /// Enter-to-commit interaction through headless key simulation, which
    /// depends on CompletionWindow's internal selection/focus behavior, not
    /// on the fix under test here.
    /// </summary>
    [AvaloniaFact]
    public void CnlCompletionData_Complete_PartialWordTyped_ReplacesPrefixNotJustCaret()
    {
        var cnlEditor = new CnlEditor { Text = "Summariz" };
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(cnlEditor);
        var data = new CnlCompletionData(new CompletionItem { Text = "Summarize", PrefixLength = 8 });
        var completionSegment = new SimpleSegment(8, 0);

        data.Complete(innerEditor.TextArea, completionSegment, EventArgs.Empty);

        Assert.Equal("Summarize", innerEditor.Text);
    }

    /// <summary>bdd-ui-interactions.md §2.23: accepting a verb completion with a SnippetText inserts the full skeleton (not just the bare verb) and leaves the caret at SnippetCursorOffset, not at the end of the inserted text.</summary>
    [AvaloniaFact]
    public void CnlCompletionData_Complete_VerbWithSnippet_InsertsSkeletonWithCaretAtOffset()
    {
        var cnlEditor = new CnlEditor { Text = "Load" };
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(cnlEditor);
        const string snippet = "Load the  from \"\".";
        var cursorOffset = snippet.IndexOf("from", StringComparison.Ordinal);
        var data = new CnlCompletionData(new CompletionItem
        {
            Text = "Load the",
            PrefixLength = 4,
            SnippetText = snippet,
            SnippetCursorOffset = cursorOffset,
        });
        var completionSegment = new SimpleSegment(4, 0);

        data.Complete(innerEditor.TextArea, completionSegment, EventArgs.Empty);

        Assert.Equal(snippet, innerEditor.Text);
        Assert.Equal(cursorOffset, innerEditor.CaretOffset);
    }

    /// <summary>bdd-ui-interactions.md §2.29, §2.35: typing a character that keeps at least one candidate matching populates CompletionItems without an explicit Ctrl+Space.</summary>
    [AvaloniaFact]
    public async Task TypingCharacter_AutoPopulatesCompletionItemsWithoutCtrlSpace()
    {
        var completionService = new FakeCompletionService
        {
            ItemsToReturn = [new CompletionItem { Text = "Summarize" }],
        };
        var tab = CreateTab(completionService: completionService);

        var cnlEditor = new CnlEditor { DataContext = tab };
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        GetInnerTextEditor(cnlEditor).TextArea.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("S");
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(tab.Editor.CompletionItems);
    }

    /// <summary>bdd-ui-interactions.md §2.36: when the completion engine returns nothing for the current position (e.g. inside a free-text span, §2.13), auto-trigger leaves CompletionItems empty rather than opening anything.</summary>
    [AvaloniaFact]
    public async Task TypingCharacter_CompletionServiceReturnsEmpty_LeavesCompletionItemsEmpty()
    {
        var tab = CreateTab();

        var cnlEditor = new CnlEditor { DataContext = tab };
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        GetInnerTextEditor(cnlEditor).TextArea.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("x");
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(tab.Editor.CompletionItems);
    }

    /// <summary>bdd-ui-interactions.md §2.33: a Backspace keystroke also triggers a fresh completion recompute, not just forward typing - proven by swapping what the (fake) engine returns between the two keystrokes and confirming the post-Backspace state reflects the new result, not a stale one.</summary>
    [AvaloniaFact]
    public async Task Backspace_TriggersCompletionRecompute()
    {
        var completionService = new FakeCompletionService
        {
            ItemsToReturn = [new CompletionItem { Text = "Summarize" }],
        };
        var tab = CreateTab(completionService: completionService);

        var cnlEditor = new CnlEditor { DataContext = tab };
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        GetInnerTextEditor(cnlEditor).TextArea.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("S");
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(tab.Editor.CompletionItems);

        completionService.ItemsToReturn = [];
        window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, string.Empty);
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(300);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(tab.Editor.CompletionItems);
    }

    /// <summary>bdd-ui-interactions.md §2.1a: CnlEditor.Undo() forwards to the real AvaloniaEdit TextEditor's own undo stack.</summary>
    [AvaloniaFact]
    public void Undo_RevertsInnerTextEditorAndViewModelText()
    {
        var cnlEditor = new CnlEditor();
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(cnlEditor);
        innerEditor.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("Load the article.");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Load the article.", innerEditor.Text);

        cnlEditor.Undo();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, innerEditor.Text);
        Assert.Equal(string.Empty, cnlEditor.Text);
    }

    /// <summary>bdd-ui-interactions.md §2.1b: CnlEditor.Redo() reapplies an edit just undone via CnlEditor.Undo().</summary>
    [AvaloniaFact]
    public void Redo_ReappliesUndoneEdit()
    {
        var cnlEditor = new CnlEditor();
        var window = new Window { Content = cnlEditor, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(cnlEditor);
        innerEditor.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("Load the article.");
        Dispatcher.UIThread.RunJobs();

        cnlEditor.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(string.Empty, innerEditor.Text);

        cnlEditor.Redo();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Load the article.", innerEditor.Text);
        Assert.Equal("Load the article.", cnlEditor.Text);
    }

    /// <summary>
    /// bdd-ui-interactions.md §7.6: undoing back to the tab's original
    /// content clears IsDirty, without any Save. Hosts the real CnlTabView
    /// (not a bare CnlEditor) so both the compiled Text binding and the
    /// UndoRequested/RedoRequested bridge (CnlTabView.axaml.cs's
    /// BindUndoRedo) are actually wired, and drives Undo through
    /// EditorViewModel.UndoCommand - the same path Ctrl+Z takes.
    /// </summary>
    [AvaloniaFact]
    public void Undo_BackToOriginalContent_ClearsIsDirty()
    {
        var tab = CreateTab();
        var tabView = new CnlTabView { DataContext = tab };
        var window = new Window { Content = tabView, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = tabView.GetVisualDescendants().OfType<TextEditor>().First();
        innerEditor.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("x");
        Dispatcher.UIThread.RunJobs();
        Assert.True(tab.IsDirty);

        tab.Editor.UndoCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(string.Empty, tab.Editor.Text);
        Assert.False(tab.IsDirty);
    }

    /// <summary>bdd-ui-interactions.md §7.6: undoing back to the most recently saved content (not just the original tab-open content) also clears IsDirty.</summary>
    [AvaloniaFact]
    public void Undo_BackToSavedContent_ClearsIsDirty()
    {
        var tab = CreateTab();
        var tabView = new CnlTabView { DataContext = tab };
        var window = new Window { Content = tabView, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = tabView.GetVisualDescendants().OfType<TextEditor>().First();
        innerEditor.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("x");
        Dispatcher.UIThread.RunJobs();
        tab.MarkAsSaved();
        Assert.False(tab.IsDirty);

        window.KeyTextInput("y");
        Dispatcher.UIThread.RunJobs();
        Assert.True(tab.IsDirty);

        tab.Editor.UndoCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("x", tab.Editor.Text);
        Assert.False(tab.IsDirty);
    }

    /// <summary>bdd-ui-interactions.md §2.1c: undo in one tab never affects another open tab's text or dirty state - each CnlTabView/CnlEditor pair is unique to its own CnlTabViewModel.</summary>
    [AvaloniaFact]
    public void Undo_InOneTab_DoesNotAffectAnotherTab()
    {
        var tabA = CreateTab();
        var tabB = CreateTab();
        var viewA = new CnlTabView { DataContext = tabA };
        var viewB = new CnlTabView { DataContext = tabB };
        var window = new Window { Content = new StackPanel { Children = { viewA, viewB } }, Width = 800, Height = 900 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerA = viewA.GetVisualDescendants().OfType<TextEditor>().First();
        var innerB = viewB.GetVisualDescendants().OfType<TextEditor>().First();

        innerA.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("a");
        Dispatcher.UIThread.RunJobs();

        innerB.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("b");
        Dispatcher.UIThread.RunJobs();

        Assert.True(tabA.IsDirty);
        Assert.True(tabB.IsDirty);

        tabA.Editor.UndoCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(tabA.IsDirty);
        Assert.Equal(string.Empty, tabA.Editor.Text);
        Assert.True(tabB.IsDirty);
        Assert.Equal("b", tabB.Editor.Text);
    }

    private static TextEditor GetInnerTextEditor(CnlEditor cnlEditor)
    {
        var textEditor = cnlEditor.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault();
        Assert.NotNull(textEditor);
        return textEditor!;
    }

    private sealed class FakePipelineService : IPipelineService
    {
        public Task<PipelineStartResult> ExplainAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = "corr" });

        public Task<PipelineStartResult> RunAsync(string source) => throw new NotImplementedException();

        public Task<PipelineStartResult> TraceAsync(string source) => throw new NotImplementedException();
    }

    private sealed class FakeDiagnosticService : IDiagnosticService
    {
        public IReadOnlyList<LocalDiagnostic> DiagnosticsToReturn { get; set; } = [];

        public IEnumerable<LocalDiagnostic> GetDiagnostics(TSNode root) => DiagnosticsToReturn;
    }

    private sealed class FakeHoverService : IHoverService
    {
        public HoverInfo? GetHover(string text, TSNode root, int cursorByte) => null;
    }

    private sealed class FakeFoldingService : IFoldingService
    {
        public IEnumerable<FoldRegion> GetFolds(TSNode root) => [];
    }

    private sealed class FakeOutlineService : IOutlineService
    {
        public IEnumerable<OutlineItem> GetOutline(string text, TSNode root) => [];
    }

    /// <summary>Untitled CnlTabViewModel wired with fakes for every native/backend collaborator, so opening it never P/Invokes the ARM64-only DLL (CLAUDE.md §3.5) or touches a real socket/HTTP client.</summary>
    private static CnlTabViewModel CreateTab(IDiagnosticService? diagnosticService = null, ICompletionService? completionService = null, IAutoPairService? autoPairService = null, INavigationService? navigationService = null) =>
        new(
            "untitled.llx",
            new FakePipelineService(),
            new FakeEventStreamService(),
            new ExecutionLockService(),
            completionService ?? new FakeCompletionService(),
            diagnosticService ?? new FakeDiagnosticService(),
            new FakeHoverService(),
            new FakeFoldingService(),
            new FakeStructuralSelectionService(),
            new FakeOutlineService(),
            autoPairService ?? new FakeAutoPairService(),
            navigationService ?? new FakeNavigationService(),
            () => new FakeParserHost());
}
