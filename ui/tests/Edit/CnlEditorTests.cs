using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using LimelightX.UI.Components;
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

    private static TextEditor GetInnerTextEditor(CnlEditor cnlEditor)
    {
        var textEditor = cnlEditor.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault();
        Assert.NotNull(textEditor);
        return textEditor!;
    }
}
