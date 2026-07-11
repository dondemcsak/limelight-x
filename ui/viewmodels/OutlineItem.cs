namespace LimelightX.UI.ViewModels;

/// <summary>One outline entry (ui-intellisense-engine-spec.md §2.5, §10) - one per CNL sentence: its leading verb, primary resource/operand text, and bound variable name (bind_stmt only).</summary>
public sealed class OutlineItem
{
    public string? Verb { get; init; }

    public string? Resource { get; init; }

    public string? Variable { get; init; }

    public required int Line { get; init; }
}
