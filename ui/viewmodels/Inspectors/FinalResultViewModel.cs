using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>
/// ui-viewmodels.md §6.6. Run mode populates from RunData.FinalResult
/// directly; Trace mode derives from the last ModelOutputBlock in
/// TraceData.ModelOutputs, since /trace has no final_result field
/// (confirmed against tests/api_trace.rs - see PipelineExecutionViewModel).
/// </summary>
public partial class FinalResultViewModel : ObservableObject
{
    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private ResultContentType _contentType = ResultContentType.PlainText;

    [ObservableProperty]
    private string _rawText = string.Empty;

    [ObservableProperty]
    private bool _isCollapsed;

    public ObservableCollection<UiError> Errors { get; } = [];

    public void Reset()
    {
        ResultText = string.Empty;
        ContentType = ResultContentType.PlainText;
        RawText = string.Empty;
        Errors.Clear();
    }
}
