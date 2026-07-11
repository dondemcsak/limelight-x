using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
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
    private static CnlTabViewModel CreateTab(IDiagnosticService? diagnosticService = null) =>
        new(
            "untitled.llx",
            new FakePipelineService(),
            new FakeEventStreamService(),
            new ExecutionLockService(),
            new FakeCompletionService(),
            diagnosticService ?? new FakeDiagnosticService(),
            new FakeHoverService(),
            new FakeFoldingService(),
            new FakeStructuralSelectionService(),
            new FakeOutlineService(),
            () => new FakeParserHost());
}
