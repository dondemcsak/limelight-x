using LimelightX.UI.Intellisense;

namespace LimelightX.UI.Tests.TestDoubles;

/// <summary>
/// No-op INavigationService for tests that need a valid TabFactory/EditorViewModel
/// dependency but don't exercise Go to Definition - mirrors FakeAutoPairService's
/// rationale. Configurable ResultToReturn for tests that do.
/// </summary>
public sealed class FakeNavigationService : INavigationService
{
    public (int Start, int End)? ResultToReturn { get; set; }

    public (int Start, int End)? FindDefinition(string text, TSNode root, int cursorByte) => ResultToReturn;
}
