namespace LimelightX.UI.ViewModels;

/// <summary>
/// Result of a PipelineExecutionViewModel call, used by the composition root
/// to decide whether to navigate to ExecutionPage or block with a guard
/// modal (bdd-ui-navigation.md: a response with no inspector data at all -
/// a transport/API-level failure - is stricter than a partial pipeline
/// failure, which still navigates and shows inline/banner errors).
/// </summary>
public enum PipelineCallOutcome
{
    NavigateToExecution,
    Blocked,
}
