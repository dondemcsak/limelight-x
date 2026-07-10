using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// App-wide singleton (ui-editor-services-guide.md §3.3,
/// ui-intellisense-engine-spec.md §5). Suggests grammar-valid next tokens at
/// a cursor position; syntactic only, never semantic
/// (cnl-editor-architecture.md §1.1.3) - empty inside free-text positions
/// (resource/target/format_target/language). Takes the raw source text (not
/// just root) because determining "what's grammar-valid here" requires
/// trial-inserting each candidate token and reparsing - a single CST alone
/// doesn't carry enough signal at a mid-edit cursor position (see
/// CompletionService's own doc comment).
/// </summary>
public interface ICompletionService
{
    IEnumerable<CompletionItem> GetCompletions(string text, TSNode root, int cursorByte);
}
