using System.Text.Json;

namespace LimelightX.UI.Services.Dto;

/// <summary>
/// ui-data-contracts.md §5.7. Shape is backend-defined and opaque to the UI,
/// so both fields are raw JsonElement rather than typed models. Markdown is
/// currently always null server-side (markdown parsing is stubbed) - render
/// Markdown output from ModelOutputBlock.RawText instead, never from here.
/// </summary>
public sealed class ParsedContent
{
    public JsonElement? Markdown { get; init; }

    public JsonElement? Json { get; init; }
}
