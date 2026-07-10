using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Components;

/// <summary>
/// Adapts a CompletionItem (EditorViewModel.CompletionItems) to AvaloniaEdit's
/// ICompletionData so CompletionWindow can render and apply it directly.
/// Complete() replaces the completion segment with Text via AvaloniaEdit's
/// own document API - the idiomatic path for its CodeCompletion mechanism.
/// EditorViewModel.SelectCompletionItemCommand is not used for this real
/// insertion path; see that command's own doc comment.
/// </summary>
public sealed class CnlCompletionData(CompletionItem item) : ICompletionData
{
    public IImage? Image => null;

    public string Text => item.Text;

    public object Content => item.Text;

    public object? Description => item.Description;

    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
        textArea.Document.Replace(completionSegment, item.Text);
}
