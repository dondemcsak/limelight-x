using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Components;

/// <summary>
/// Adapts a CompletionItem (EditorViewModel.CompletionItems) to AvaloniaEdit's
/// ICompletionData so CompletionWindow can render and apply it directly.
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

    /// <summary>
    /// Replaces exactly item.PrefixLength already-typed characters ending at
    /// the caret with item.SnippetText (if set - bdd-ui-interactions.md
    /// §2.23) or otherwise item.Text (§2.34) - deliberately ignores
    /// completionSegment's own StartOffset. AvaloniaEdit's CompletionWindow
    /// has one shared StartOffset for the whole window session, but
    /// different simultaneously-listed items can have matched different
    /// prefix lengths (CompletionService.MatchPrefix is computed
    /// per-candidate), so a single shared replacement range can't correctly
    /// serve every item - computing this item's own start from
    /// completionSegment.EndOffset (which AvaloniaEdit does track live as
    /// the caret) minus its own PrefixLength sidesteps that entirely. When a
    /// snippet has a SnippetCursorOffset, the caret lands inside the
    /// inserted skeleton (e.g. right before "from") instead of at its end.
    /// </summary>
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var insertText = item.SnippetText ?? item.Text;
        var replaceStart = completionSegment.EndOffset - item.PrefixLength;
        var segment = new SimpleSegment(replaceStart, item.PrefixLength);
        textArea.Document.Replace(segment, insertText);

        textArea.Caret.Offset = item.SnippetCursorOffset is { } offset
            ? replaceStart + offset
            : replaceStart + insertText.Length;
    }
}
