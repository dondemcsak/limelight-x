using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// App-wide singleton (ui-editor-services-guide.md §3.4). Advisory local
/// diagnostics from ERROR/MISSING CST nodes - never authoritative, never
/// written to EditorViewModel.SyntaxErrors (cnl-editor-architecture.md §5;
/// bdd-ui-interactions.md §2.8).
/// </summary>
public interface IDiagnosticService
{
    IEnumerable<LocalDiagnostic> GetDiagnostics(TSNode root);
}
