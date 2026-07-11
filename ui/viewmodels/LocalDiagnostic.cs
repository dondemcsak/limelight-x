namespace LimelightX.UI.ViewModels;

/// <summary>
/// One advisory entry for EditorViewModel.LocalDiagnostics (ui-viewmodels.md
/// §6, bdd-ui-interactions.md §2.7-§2.8) - a Tree-sitter ERROR/MISSING node's
/// span. Never authoritative and never written into SyntaxErrors
/// (cnl-editor-architecture.md §5) - EditorViewModel.SyntaxErrors comes
/// exclusively from /explain. SuggestedFix is non-null only for the fixed
/// set of self-describing MISSING literals (bdd-ui-interactions.md §2.18,
/// ui-intellisense-engine-spec.md §6.1) - null for every other diagnostic.
/// </summary>
public readonly record struct LocalDiagnostic(string Message, int StartByte, int EndByte, string? SuggestedFix = null);
