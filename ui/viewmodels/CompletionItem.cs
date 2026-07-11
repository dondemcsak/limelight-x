namespace LimelightX.UI.ViewModels;

/// <summary>One entry for EditorViewModel.CompletionItems (ui-viewmodels.md §4.1), returned directly by ICompletionService.GetCompletions - no separate CompletionResult wrapper type.</summary>
public sealed class CompletionItem
{
    public required string Text { get; init; }

    public string? Description { get; init; }
}
