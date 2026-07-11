using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using LimelightX.UI.Components;
using LimelightX.UI.Intellisense;
using LimelightX.UI.Services;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.Tests.TestDoubles;
using LimelightX.UI.ViewModels.Tabs;
using Xunit;

namespace LimelightX.UI.Tests.Edit;

/// <summary>
/// Reproduces the user's manual-testing regression report against the real,
/// fully-wired Tree-sitter-backed services (matching App.axaml.cs's
/// composition root exactly, not the fakes CnlEditorTests.cs uses), driven
/// by real simulated keystrokes rather than direct Text assignment - the
/// theory being that DiagnosticServiceTests.cs's unit-level missing-period
/// test still passes, so if a regression exists it must live in the
/// UI-wiring/auto-trigger-completion interaction, not DiagnosticService
/// itself.
/// </summary>
[Trait("Category", "NativeArm64")]
public class CnlEditorLiveTypingTests
{
    [AvaloniaFact]
    public async Task TypingSentenceWithoutTrailingPeriod_StillFlagsMissingPeriodDiagnosticAndGhostSuggestion()
    {
        var tab = CreateRealTab();

        // Hosting the real CnlTabView (not a bare CnlEditor with only
        // DataContext set) so the compiled Text="{Binding Editor.Text}"
        // two-way binding actually exists - a bare CnlEditor never
        // establishes that binding on its own (CnlEditor.axaml.cs's own
        // constructor never touches EditorViewModel.Text directly; only
        // CnlTabView.axaml's compiled binding does).
        var tabView = new CnlTabView { DataContext = tab };
        var window = new Window { Content = tabView, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(tabView);
        innerEditor.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();

        const string sentence = "Summarize the article";
        foreach (var ch in sentence)
        {
            window.KeyTextInput(ch.ToString());
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(250);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(sentence, innerEditor.Text);
        Assert.Equal(sentence, tab.Editor.Text);
        Assert.Contains(tab.Editor.LocalDiagnostics, d => d.Message.Contains("period", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tab.Editor.GhostSuggestion);
    }

    /// <summary>
    /// Regression test for the reported bug: the user's sentence ends in
    /// "it" - itself a valid, already-fully-typed pronoun completion
    /// candidate (CompletionService.Pronouns) - so before the fix, the
    /// auto-trigger debounce (§2.29/§2.35) left a no-op completion popup
    /// (offering to replace "it" with "it") open right at the caret,
    /// visually covering the missing-period squiggle/ghost text
    /// (§2.18) at that same position even though LocalDiagnostics/
    /// GhostSuggestion were computed correctly underneath it. Confirmed via
    /// CompletionWindow being null here - CnlEditor.RequestCompletionsAndUpdateWindow's
    /// isAutoTriggered branch now treats "every candidate's PrefixLength
    /// already equals its full length" the same as "no candidates" and never
    /// opens/leaves open a window for it.
    /// </summary>
    [AvaloniaFact]
    public async Task TypingSentenceEndingInPronoun_MissingPeriodFlagged_AndNoNoOpCompletionWindowObscuresIt()
    {
        var tab = CreateRealTab();
        var tabView = new CnlTabView { DataContext = tab };
        var window = new Window { Content = tabView, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(tabView);
        innerEditor.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();

        const string sentence = "Summarize it";
        foreach (var ch in sentence)
        {
            window.KeyTextInput(ch.ToString());
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(250);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(sentence, tab.Editor.Text);
        Assert.Contains(tab.Editor.LocalDiagnostics, d => d.Message.Contains("period", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tab.Editor.GhostSuggestion);
        Assert.Null(GetCompletionWindowField(tabView));
    }

    private static object? GetCompletionWindowField(CnlTabView tabView)
    {
        var cnlEditor = tabView.GetVisualDescendants().OfType<CnlEditor>().FirstOrDefault();
        Assert.NotNull(cnlEditor);
        var field = typeof(CnlEditor).GetField("_completionWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        return field!.GetValue(cnlEditor);
    }

    private static TextEditor GetInnerTextEditor(CnlTabView tabView)
    {
        var textEditor = tabView.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault();
        Assert.NotNull(textEditor);
        return textEditor!;
    }

    private sealed class FakePipelineService : IPipelineService
    {
        public Task<PipelineStartResult> ExplainAsync(string source) => Task.FromResult(new PipelineStartResult { Accepted = true, CorrelationId = "corr" });

        public Task<PipelineStartResult> RunAsync(string source) => throw new NotImplementedException();

        public Task<PipelineStartResult> TraceAsync(string source) => throw new NotImplementedException();
    }

    private static CnlTabViewModel CreateRealTab()
    {
        var queryRunner = new QueryRunner();
        return new CnlTabViewModel(
            "untitled.llx",
            new FakePipelineService(),
            new FakeEventStreamService(),
            new ExecutionLockService(),
            new CompletionService(),
            new DiagnosticService(),
            new HoverService(),
            new FoldingService(queryRunner),
            new StructuralSelectionService(),
            new OutlineService(),
            new AutoPairService(),
            new NavigationService(),
            () => new ParserHost());
    }
}
