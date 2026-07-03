namespace LimelightX.UI.ViewModels;

/// <summary>
/// Placeholder shape for EditorViewModel.CompletionItems (ui-viewmodels.md
/// §4.1). The grammar-driven completion engine itself is deferred to Phase
/// 4b (see EditorViewModel's class comment) - this type exists so the
/// ViewModel's state shape matches spec now, ahead of that engine landing.
/// </summary>
public sealed class CompletionItem
{
    public required string Text { get; init; }

    public string? Description { get; init; }
}
