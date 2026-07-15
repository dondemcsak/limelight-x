namespace LimelightX.UI.ViewModels;

/// <summary>One collapsible region for EditorViewModel.FoldRegions (ui-viewmodels.md §6, bdd-ui-interactions.md §2.9) - one per CNL sentence, UTF-8 byte offsets from the CST.</summary>
public readonly record struct FoldRegion(int StartByte, int EndByte);
