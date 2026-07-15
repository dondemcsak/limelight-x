namespace LimelightX.UI.Services.Dto;

/// <summary>ui-data-contracts.md §2, §5.</summary>
public sealed class RawAstResponse
{
    public required AstNode Root { get; init; }

    public required string RawText { get; init; }

    public required AstMetadata Metadata { get; init; }
}
