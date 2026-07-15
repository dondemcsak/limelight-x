using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;

namespace LimelightX.UI.Components;

/// <summary>
/// Injects EditorViewModel.GhostSuggestion's InsertText as a non-editable,
/// semi-transparent inline element at InsertionByte (converted to a UTF-16
/// char offset by CnlEditor - bdd-ui-interactions.md §2.18). Unlike
/// LocalDiagnosticsRenderer/DiagnosticMarginMarker (IBackgroundRenderer/
/// AbstractMargin, which only decorate around existing text),
/// VisualLineElementGenerator is required here because ghost text must
/// inject new inline content that real document content flows around,
/// without mutating EditorViewModel.Text - see ApplyQuickFixCommand
/// (§2.19) for the actual commit.
/// </summary>
public sealed class GhostTextElementGenerator(IBrush brush) : VisualLineElementGenerator
{
    /// <summary>UTF-16 char offset to inject InsertText at, or null when there is no active ghost suggestion.</summary>
    public int? InsertionOffset { get; set; }

    public string InsertText { get; set; } = string.Empty;

    public override int GetFirstInterestedOffset(int startOffset)
    {
        if (InsertionOffset is not { } offset || string.IsNullOrEmpty(InsertText) || offset < startOffset)
        {
            return -1;
        }

        return offset;
    }

    public override VisualLineElement ConstructElement(int offset) =>
        offset == InsertionOffset ? new GhostTextVisualLineElement(InsertText, brush) : null!;

    private sealed class GhostTextVisualLineElement(string text, IBrush brush) : VisualLineElement(1, 0)
    {
        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            var properties = new VisualLineElementTextRunProperties(context.GlobalTextRunProperties);
            properties.SetForegroundBrush(brush);
            return new TextCharacters(text, properties);
        }
    }
}
