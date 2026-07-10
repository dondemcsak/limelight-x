using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace LimelightX.UI.Components;

/// <summary>
/// Highlights each EditorViewModel.LocalDiagnostics span (bdd-ui-interactions.md
/// §2.7-§2.8) with a translucent background wash - advisory only, distinct
/// from ValidationOverlay's /explain-driven error list (which, per its own
/// doc comment, can't render in-text markers yet since the backend's AST
/// spans are still {0,0} placeholders). Segment offsets are supplied
/// pre-converted to UTF-16 char offsets by CnlEditor.
/// </summary>
public sealed class LocalDiagnosticsRenderer(Color color) : IBackgroundRenderer
{
    private readonly IBrush _brush = new SolidColorBrush(color, 0.25);

    public IReadOnlyList<(int Start, int End)> Diagnostics { get; set; } = [];

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Diagnostics.Count == 0 || !textView.VisualLinesValid)
        {
            return;
        }

        var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true };
        foreach (var (start, end) in Diagnostics)
        {
            if (end > start)
            {
                builder.AddSegment(textView, new SimpleSegment(start, end - start));
            }
        }

        var geometry = builder.CreateGeometry();
        if (geometry is not null)
        {
            drawingContext.DrawGeometry(_brush, null, geometry);
        }
    }
}
