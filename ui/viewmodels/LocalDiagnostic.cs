namespace LimelightX.UI.ViewModels;

/// <summary>
/// One advisory entry for EditorViewModel.LocalDiagnostics (ui-viewmodels.md
/// §6, bdd-ui-interactions.md §2.7-§2.8) - a Tree-sitter ERROR/MISSING node's
/// span. Never authoritative and never written into SyntaxErrors
/// (cnl-editor-architecture.md §5) - EditorViewModel.SyntaxErrors comes
/// exclusively from /explain.
/// </summary>
public readonly record struct LocalDiagnostic(string Message, int StartByte, int EndByte);
