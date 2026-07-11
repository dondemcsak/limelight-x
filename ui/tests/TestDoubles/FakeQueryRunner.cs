using LimelightX.UI.Intellisense;

namespace LimelightX.UI.Tests.TestDoubles;

/// <summary>
/// No-op IQueryRunner for tests that need a valid FoldingService dependency
/// but don't exercise IntelliSense behavior (e.g. TabFactory wiring in
/// Workspace/Execution tests) - avoids pulling in the real, native-DLL-backed
/// QueryRunner (ui/intellisense/QueryRunner.cs) outside the NativeArm64-gated
/// suite.
/// </summary>
public sealed class FakeQueryRunner : IQueryRunner
{
    public IEnumerable<QueryMatch> RunHighlights(TSNode root) => [];

    public IEnumerable<QueryMatch> RunFolds(TSNode root) => [];

    public IEnumerable<QueryMatch> RunInjections(TSNode root) => [];

    public void Dispose()
    {
    }
}
