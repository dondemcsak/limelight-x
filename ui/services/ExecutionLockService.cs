namespace LimelightX.UI.Services;

/// <summary>Concrete IExecutionLockService. Constructed once in the composition root (App.axaml.cs).</summary>
public sealed class ExecutionLockService : IExecutionLockService
{
    private object? _holder;

    public bool IsAnyExecutionRunning => _holder is not null;

    public event Action? ExecutionLockChanged;

    public bool TryAcquire(object token)
    {
        if (_holder is not null)
        {
            return false;
        }

        _holder = token;
        ExecutionLockChanged?.Invoke();
        return true;
    }

    public void Release(object token)
    {
        if (!ReferenceEquals(_holder, token))
        {
            // Not the current holder (already released, or never acquired) - no-op.
            return;
        }

        _holder = null;
        ExecutionLockChanged?.Invoke();
    }
}
