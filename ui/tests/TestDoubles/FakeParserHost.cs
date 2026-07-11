using LimelightX.UI.Intellisense;

namespace LimelightX.UI.Tests.TestDoubles;

/// <summary>
/// No-op IParserHost for tests that need a valid TabFactory/CnlTabViewModel
/// dependency but don't exercise IntelliSense behavior - avoids pulling in
/// the real, native-DLL-backed ParserHost (ui/intellisense/ParserHost.cs)
/// outside the NativeArm64-gated suite. Mirrors FakeQueryRunner's rationale;
/// this is the fix for the CI gating gap found in review: CnlTabViewModel
/// used to hardcode `new ParserHost()` with no injection seam, so every
/// Workspace/Execution test that opened a .llx file silently loaded the
/// real ARM64-only DLL on the windows-latest (x64) CI runner.
/// </summary>
public sealed class FakeParserHost : IParserHost
{
    public TSNode Parse(string text) => default;

    public void Dispose()
    {
    }
}
