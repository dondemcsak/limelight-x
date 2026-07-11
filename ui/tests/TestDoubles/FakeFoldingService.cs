using LimelightX.UI.Intellisense;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Tests.TestDoubles;

/// <summary>
/// No-op IFoldingService for tests that need a valid TabFactory dependency
/// but don't exercise folding behavior - mirrors FakeCompletionService's
/// rationale. Same native-crash-avoidance reasoning as FakeDiagnosticService
/// applies here (pairing the real FoldingService with FakeParserHost's
/// default TSNode is unsafe).
/// </summary>
public sealed class FakeFoldingService : IFoldingService
{
    public IEnumerable<FoldRegion> GetFolds(TSNode root) => [];
}
