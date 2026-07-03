using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using LimelightX.UI.Components;
using LimelightX.UI.Services;
using LimelightX.UI.ViewModels;
using LimelightX.UI.Views;
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
/// </summary>
public class CnlEditorTests
{
    private sealed class FakePipelineService : IPipelineService
    {
        public Task<ExplainResult> ExplainAsync(string source) =>
            Task.FromResult(new ExplainResult { Success = true });

        public Task<RunResult> RunAsync(string source) => throw new NotImplementedException();

        public Task<TraceResult> TraceAsync(string source) => throw new NotImplementedException();
    }

    [AvaloniaFact]
    public void SettingViewModelText_BeforeAttach_PropagatesToAvaloniaEditOnceAttached()
    {
        var editorViewModel = new EditorViewModel(new FakePipelineService());
        var editorPage = new EditorPage { DataContext = editorViewModel };

        editorViewModel.Text = "Load the article from \"a.txt\".\nSummarize it.";

        var window = new Window { Content = editorPage, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(editorPage);
        Assert.Equal(editorViewModel.Text, innerEditor.Text);
    }

    [AvaloniaFact]
    public void SettingViewModelText_AfterAttach_PropagatesToAvaloniaEditImmediately()
    {
        var editorViewModel = new EditorViewModel(new FakePipelineService());
        var editorPage = new EditorPage { DataContext = editorViewModel };
        var window = new Window { Content = editorPage, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        editorViewModel.Text = "Load the article from \"a.txt\".\nSummarize it.";
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(editorPage);
        Assert.Equal(editorViewModel.Text, innerEditor.Text);
    }

    [AvaloniaFact]
    public void TextEditor_HasTemplateApplied()
    {
        var editorPage = new EditorPage { DataContext = new EditorViewModel(new FakePipelineService()) };
        var window = new Window { Content = editorPage, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var innerEditor = GetInnerTextEditor(editorPage);

        // A missing control theme (the Phase 4 bug) leaves the TextArea with no
        // visual children at all - this would have caught it directly.
        Assert.NotNull(innerEditor.TextArea);
        Assert.True(innerEditor.GetVisualDescendants().Any(), "TextEditor has no visual children - its control theme is likely not merged into App.axaml.");
    }

    private static TextEditor GetInnerTextEditor(EditorPage editorPage)
    {
        var cnlEditor = editorPage.GetVisualDescendants().OfType<CnlEditor>().FirstOrDefault();
        Assert.NotNull(cnlEditor);

        var textEditor = cnlEditor!.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault();
        Assert.NotNull(textEditor);
        return textEditor!;
    }
}
