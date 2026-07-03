namespace LimelightX.UI.ViewModels;

/// <summary>
/// Placeholder shape for EditorViewModel.UndoStack/RedoStack (ui-viewmodels.md
/// §4.1). Both stacks stay empty in practice: real undo/redo state lives in
/// AvaloniaEdit's own TextDocument.UndoStack (battle-tested, per-keystroke
/// granular, and already exposed by CnlEditor's underlying TextEditor control)
/// - EditorViewModel's UndoCommand/RedoCommand simply request that CnlEditor
/// invoke it (see EditorViewModel.UndoRequested/RedoRequested). Reimplementing
/// a parallel whole-text-snapshot undo stack here would just be duplicate,
/// divergent state, which ui-viewmodels.md §8's determinism rules disallow.
/// </summary>
public sealed class EditorAction
{
    public required string Description { get; init; }
}
