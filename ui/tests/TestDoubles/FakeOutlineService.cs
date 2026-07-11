using LimelightX.UI.Intellisense;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Tests.TestDoubles;

/// <summary>
/// No-op IOutlineService for tests that need a valid TabFactory dependency
/// but don't exercise outline behavior - mirrors FakeCompletionService's
/// rationale. Same native-crash-avoidance reasoning as FakeDiagnosticService
/// applies here (pairing the real OutlineService with FakeParserHost's
/// default TSNode is unsafe).
/// </summary>
public sealed class FakeOutlineService : IOutlineService
{
    public IEnumerable<OutlineItem> GetOutline(string text, TSNode root) => [];
}
