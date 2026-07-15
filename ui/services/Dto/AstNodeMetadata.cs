namespace LimelightX.UI.Services.Dto;

/// <summary>Per-node metadata (ui-data-contracts.md §5.1). Resource/Pronoun are optional.</summary>
public sealed class AstNodeMetadata
{
    public string? Resource { get; init; }

    public string? Pronoun { get; init; }

    public bool ExpressionHole { get; init; }

    public bool Normalized { get; init; }
}
