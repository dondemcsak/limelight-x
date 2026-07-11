using LimelightX.UI.ViewModels;

namespace LimelightX.UI.Intellisense;

/// <summary>
/// App-wide singleton (ui-editor-services-guide.md §3.7). One OutlineItem per
/// top-level `sentence` node - syntactic, CST-only, never semantic
/// (cnl-editor-architecture.md §1.1.3). Takes the raw source text (not just
/// root), same reasoning as ICompletionService/IHoverService: Resource/
/// Variable are free-text/name node spans, and node type alone only gives
/// their span, not their content.
/// </summary>
public interface IOutlineService
{
    IEnumerable<OutlineItem> GetOutline(string text, TSNode root);
}
