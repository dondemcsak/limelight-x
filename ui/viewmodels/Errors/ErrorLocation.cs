using LimelightX.UI.Services.Dto;

namespace LimelightX.UI.ViewModels.Errors;

/// <summary>
/// ui-data-contracts.md §1 error object "location" field. Optional - parser/
/// normalizer failures populate it when available, other error classes omit it.
/// </summary>
public sealed class ErrorLocation
{
    public int Line { get; init; }

    public int Column { get; init; }

    public Span? Span { get; init; }
}
