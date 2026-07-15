using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Services.Dto;

/// <summary>
/// Shared response envelope (ui-data-contracts.md §1). Data is nullable since
/// it may be omitted when Success is false.
/// </summary>
public sealed class Envelope<TData>
{
    public required string Version { get; init; }

    public required bool Success { get; init; }

    public IReadOnlyList<UiError> Errors { get; init; } = [];

    public TData? Data { get; init; }
}
