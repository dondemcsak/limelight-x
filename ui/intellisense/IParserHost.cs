namespace LimelightX.UI.Intellisense;

/// <summary>
/// Per-tab wrapper over the Tree-sitter parser/tree lifecycle for
/// tree-sitter-limelightx.dll (spec/parsing/tree-sitter-integration.md).
/// One instance per open .llx tab, constructed in CnlTabViewModel - not a
/// composition-root singleton, since each open document needs its own CST.
/// Client-side only: never calls, and is never called by, /src/api or Rust
/// (cnl-editor-architecture.md §5).
/// </summary>
public interface IParserHost : IDisposable
{
    /// <summary>Reparses the given text, replacing any previous tree. Returns the new tree's root node.</summary>
    TSNode Parse(string text);
}
