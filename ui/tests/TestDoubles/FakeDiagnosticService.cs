using LimelightX.UI.Intellisense;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Tests.TestDoubles;

/// <summary>
/// No-op IDiagnosticService for tests that need a valid TabFactory
/// dependency but don't exercise diagnostic behavior - mirrors
/// FakeCompletionService's rationale. Needed now that EditorViewModel.
/// RefreshDecorations() runs on every Text change (bdd-ui-interactions.md
/// §2.7a), not just on explicit calls: pairing the real, native-backed
/// DiagnosticService with FakeParserHost (which returns a default/zeroed
/// TSNode) crashes - ts_node_is_missing on a default TSNode is an access
/// violation, not a managed exception.
/// </summary>
public sealed class FakeDiagnosticService : IDiagnosticService
{
    public IEnumerable<LocalDiagnostic> GetDiagnostics(TSNode root) => [];
}
