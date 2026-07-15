namespace LimelightX.UI.Services;

/// <summary>
/// App-wide execution lock (ui-viewmodels.md §8): only one .llx tab may have
/// a Run/Explain execution in flight at a time. Gates every tab's
/// EditorViewModel.CanExecute and WorkspaceViewModel.OpenSettingsCommand.
/// Does not gate tab switching, tab open/close, or folder-tree browsing -
/// those never call into this service.
///
/// Not thread-safe by design: like the rest of this app, every caller
/// (PipelineExecutionViewModel) mutates state on the Avalonia UI thread,
/// which is where WsEvents are already marshaled via Dispatcher.UIThread.Post
/// (see EventStreamService).
/// </summary>
public interface IExecutionLockService
{
    bool IsAnyExecutionRunning { get; }

    /// <summary>Succeeds only if no other token currently holds the lock.</summary>
    bool TryAcquire(object token);

    /// <summary>No-op if <paramref name="token"/> does not hold the lock (already released, or never acquired).</summary>
    void Release(object token);

    event Action? ExecutionLockChanged;
}
