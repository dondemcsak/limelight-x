using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
public sealed partial class CnlTabViewModel : TabViewModel
{
    public CnlTabViewModel(
        string filePath,
        string initialText,
        IPipelineService pipelineService,
        IEventStreamService eventStream,
        IExecutionLockService executionLock)
        : base(filePath, Path.GetFileName(filePath))
    {
        Editor = new EditorViewModel(pipelineService, eventStream, executionLock) { Text = initialText };
        PipelineExecution = new PipelineExecutionViewModel(pipelineService, eventStream, executionLock);

        Editor.RunRequested = PipelineExecution.RunPipelineAsync;
        Editor.ExplainRequested = PipelineExecution.ExplainPipelineAsync;

        // Only track dirtiness after the initial load above - opening a file
        // must never itself mark the tab dirty.
        Editor.PropertyChanged += OnEditorPropertyChanged;
    }

    /// <summary>Untitled-tab constructor (File > New LLX File, ui-viewmodels.md §3) - no backing file, starts with empty text and IsDirty == false.</summary>
    public CnlTabViewModel(
        string header,
        IPipelineService pipelineService,
        IEventStreamService eventStream,
        IExecutionLockService executionLock)
        : base(null, header)
    {
        Editor = new EditorViewModel(pipelineService, eventStream, executionLock) { Text = string.Empty };
        PipelineExecution = new PipelineExecutionViewModel(pipelineService, eventStream, executionLock);

        Editor.RunRequested = PipelineExecution.RunPipelineAsync;
        Editor.ExplainRequested = PipelineExecution.ExplainPipelineAsync;

        Editor.PropertyChanged += OnEditorPropertyChanged;
    }

    public EditorViewModel Editor { get; }

    public PipelineExecutionViewModel PipelineExecution { get; }

    /// <summary>
    /// This tab's editor/execution-panel split ratio (ui-viewmodels.md §5.2),
    /// adjusted by dragging the splitter in CnlTabView. Tab-scoped,
    /// session-only state - not persisted to disk.
    /// </summary>
    [ObservableProperty]
    private double _editorPaneRatio = 0.5;

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
