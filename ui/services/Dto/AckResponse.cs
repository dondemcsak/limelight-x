namespace LimelightX.UI.Services.Dto;

/// <summary>
/// The immediate synchronous response to POST /run|/explain|/trace
/// (api.md §2.1) - actual results arrive later as <see cref="WsEvent"/>s.
/// </summary>
public sealed class AckResponse
{
    public required bool Accepted { get; init; }

    public required string CorrelationId { get; init; }
}
