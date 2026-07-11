using LimelightX.UI.Intellisense;
using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Tests.TestDoubles;

/// <summary>
/// No-op ICompletionService for tests that need a valid TabFactory
/// dependency but don't exercise completion behavior - mirrors
/// FakeQueryRunner's rationale. Unlike DiagnosticService/HoverService (both
/// stateless), the real CompletionService eagerly constructs its own
/// private ParserHost in a field initializer, so `new CompletionService()`
/// alone P/Invokes the native DLL - this fake exists specifically to avoid
/// that outside the NativeArm64-gated suite (CLAUDE.md §3.5).
/// </summary>
public sealed class FakeCompletionService : ICompletionService
{
    public IReadOnlyList<CompletionItem> ItemsToReturn { get; set; } = [];

    public IEnumerable<CompletionItem> GetCompletions(string text, TSNode root, int cursorByte) => ItemsToReturn;
}
