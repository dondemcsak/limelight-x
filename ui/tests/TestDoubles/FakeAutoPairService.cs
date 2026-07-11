using LimelightX.UI.Intellisense;

namespace LimelightX.UI.Tests.TestDoubles;

/// <summary>
/// No-op IAutoPairService for tests that need a valid TabFactory/EditorViewModel
/// dependency but don't exercise auto-closing-pair behavior - mirrors
/// FakeHoverService's rationale. Configurable ResultToReturn for tests that do.
/// </summary>
public sealed class FakeAutoPairService : IAutoPairService
{
    public bool ResultToReturn { get; set; }

    public bool CanAutoClose(string text, TSNode root, int cursorByte, string opener) => ResultToReturn;
}
