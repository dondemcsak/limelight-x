using LimelightX.UI.Intellisense;

namespace LimelightX.UI.Tests.TestDoubles;

/// <summary>
/// No-op IStructuralSelectionService for tests that need a valid TabFactory
/// dependency but don't exercise structural selection - mirrors
/// FakeQueryRunner's rationale.
/// </summary>
public sealed class FakeStructuralSelectionService : IStructuralSelectionService
{
    public (int Start, int End) ExpandSelection(TSNode root, int startByte, int endByte) => (startByte, endByte);
}
