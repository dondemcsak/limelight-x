using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// App-wide singleton (ui-editor-services-guide.md §3.5). Hover content is a
/// syntactic, CST-only, best-effort local echo (cnl-editor-architecture.md
/// §1.1.3) - never authoritative. Returns null (not an Empty sentinel) when
/// there is no hover content at the given position. Takes the raw source
/// text (not just root) because variable-binding hover (ui-intellisense-engine-spec.md
/// §7.1) needs to extract the matched bind_stmt's source span, not just walk node types.
/// </summary>
public interface IHoverService
{
    HoverInfo? GetHover(string text, TSNode root, int cursorByte);
}
