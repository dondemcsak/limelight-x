namespace LimelightX.UI.Services.Dto;

/// <summary>POST /explain `data` payload (ui-data-contracts.md §2).</summary>
public sealed class ExplainData
{
    public required RawAstResponse RawAst { get; init; }

    public required NormalizedAstResponse NormalizedAst { get; init; }
}
