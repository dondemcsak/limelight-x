using LimelightX.UI.Intellisense;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Tests.TestDoubles;

/// <summary>
/// No-op IHoverService for tests that need a valid TabFactory dependency but
/// don't exercise hover behavior - mirrors FakeCompletionService's
/// rationale. Same native-crash-avoidance reasoning as FakeDiagnosticService
/// applies here (pairing the real HoverService with FakeParserHost's default
/// TSNode is unsafe).
/// </summary>
public sealed class FakeHoverService : IHoverService
{
    public HoverInfo? GetHover(string text, TSNode root, int cursorByte) => null;
}
