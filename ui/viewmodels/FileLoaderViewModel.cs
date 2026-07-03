using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LimelightX.UI.Services;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels;

/// <summary>
/// Opens .llx files and tracks recent files (ui-viewmodels.md §3.2). Emits
/// loaded content via FileLoaded rather than a direct EditorViewModel
/// reference - EditorViewModel doesn't exist until Phase 4, and this keeps
/// FileLoaderViewModel from depending on a sibling ViewModel; the
/// composition root wires the event once both exist.
/// </summary>
public partial class FileLoaderViewModel : ObservableObject
{
    private readonly IFilePickerService _filePicker;

    public FileLoaderViewModel(IFilePickerService filePicker)
    {
        _filePicker = filePicker;
    }

    [ObservableProperty]
    private string? _selectedFilePath;

    [ObservableProperty]
    private string? _fileContent;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<string> RecentFiles { get; } = [];

    public ObservableCollection<UiError> Errors { get; } = [];

    /// <summary>Raised with the file's text content after a successful load.</summary>
    public event Action<string>? FileLoaded;

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = await _filePicker.PickCnlFileAsync();
        if (path is not null)
        {
            await LoadFileAsync(path);
        }
    }

    [RelayCommand]
    private async Task LoadFileAsync(string path)
    {
        Errors.Clear();
        IsLoading = true;
        try
        {
            if (!File.Exists(path))
            {
                Errors.Add(new ValidationError
                {
                    Code = "ERR_FILE_NOT_FOUND",
                    Message = $"File not found: {path}",
                    Severity = ErrorSeverity.Error,
                    Category = ErrorCategory.Validation,
                });
                return;
            }

            var content = await File.ReadAllTextAsync(path);

            FileContent = content;
            SelectedFilePath = path;

            RecentFiles.Remove(path);
            RecentFiles.Insert(0, path);

            FileLoaded?.Invoke(content);
        }
        catch (IOException ex)
        {
            Errors.Add(new ValidationError
            {
                Code = "ERR_FILE_READ",
                Message = ex.Message,
                Severity = ErrorSeverity.Error,
                Category = ErrorCategory.Validation,
            });
        }
        finally
        {
            IsLoading = false;
        }
    }
}
