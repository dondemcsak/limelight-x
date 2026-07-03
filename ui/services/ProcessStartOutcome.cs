namespace LimelightX.UI.Services;

/// <summary>Result of LlxProcessService.StartAsync.</summary>
public sealed record ProcessStartOutcome(bool Success, string? ErrorMessage);
