using System.ComponentModel;
using LimelightX.UI.Services;

namespace LimelightX.UI.ViewModels.Tabs;

/// <summary>
/// Tab for an open .llx file (ui-viewmodels.md §5.2). Owns one
/// EditorViewModel and one PipelineExecutionViewModel, both per-tab
/// instances constructed here (not composition-root singletons) - wiring
/// Editor.RunRequested/ExplainRequested directly to this tab's own
/// PipelineExecution is simpler than the old app-wide Func-wiring done in
/// App.axaml.cs, since it's naturally 1:1 per tab now.
/// </summary>
public sealed class CnlTabViewModel : TabViewModel
{
    public CnlTabViewModel(
        string filePath,
        string initialText,
        IPipelineService pipelineService,
        IEventStreamService eventStream,
        IExecutionLockService executionLock)
        : base(filePath)
    {
        Editor = new EditorViewModel(pipelineService, eventStream, executionLock) { Text = initialText };
        PipelineExecution = new PipelineExecutionViewModel(pipelineService, eventStream, executionLock);

        Editor.RunRequested = PipelineExecution.RunPipelineAsync;
        Editor.ExplainRequested = PipelineExecution.ExplainPipelineAsync;

        // Only track dirtiness after the initial load above - opening a file
        // must never itself mark the tab dirty.
        Editor.PropertyChanged += OnEditorPropertyChanged;
    }

    public EditorViewModel Editor { get; }

    public PipelineExecutionViewModel PipelineExecution { get; }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.Text))
        {
            IsDirty = true;
        }
    }

    public override void Dispose()
    {
        Editor.PropertyChanged -= OnEditorPropertyChanged;
        Editor.Dispose();
        PipelineExecution.Dispose();
    }
}
