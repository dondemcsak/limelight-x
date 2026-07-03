using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Services;

/// <summary>Concrete ILogService: a fixed-size in-memory ring buffer, nothing persisted.</summary>
public sealed class LogService(int capacity = 200) : ILogService
{
    private readonly LinkedList<UiError> _entries = new();
    private readonly object _lock = new();

    public IReadOnlyList<UiError> Entries
    {
        get
        {
            lock (_lock)
            {
                return _entries.ToArray();
            }
        }
    }

    public void Log(UiError error)
    {
        lock (_lock)
        {
            _entries.AddLast(error);
            while (_entries.Count > capacity)
            {
                _entries.RemoveFirst();
            }
        }
    }
}
