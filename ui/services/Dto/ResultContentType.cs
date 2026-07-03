namespace LimelightX.UI.Services.Dto;

/// <summary>
/// Wire values "plain"|"markdown"|"json" (ui-data-contracts.md §4, §5.7,
/// §5.8), deserialized case-insensitively via PipelineJsonOptions. Named
/// "Plain" here (not "PlainText") to match the wire word directly under the
/// snake_case-lower naming policy; ui-viewmodels.md §6.6's ResultContentType
/// enum names it "PlainText" for the ViewModel-facing type - that mapping
/// happens in FinalResultViewModel (Phase 5), not here.
/// </summary>
public enum ResultContentType
{
    Plain,
    Markdown,
    Json,
}
