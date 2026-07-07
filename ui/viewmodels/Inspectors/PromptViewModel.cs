using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LimelightX.UI.Services.Dto;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>ui-viewmodels.md §6.4. Trace-only.</summary>
public partial class PromptViewModel : ObservableObject
{
    public ObservableCollection<PromptBlock> Prompts { get; } = [];

    [ObservableProperty]
    private string _rawText = string.Empty;

    [ObservableProperty]
    private bool _isCollapsed;

    public ObservableCollection<UiError> Errors { get; } = [];

    public PromptViewModel()
    {
        Errors.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasErrors));
        Prompts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPrompts));
    }

    /// <summary>Set when this inspector's own data fails to render (ui-error-handling.md §6.3).</summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>Whether the panel should be shown yet - true from the first `prompt_generated` event, false again after <see cref="Reset"/> (ui-components.md §6.5).</summary>
    public bool HasPrompts => Prompts.Count > 0;

    public void Reset()
    {
        Prompts.Clear();
        RawText = string.Empty;
        Errors.Clear();
    }
}
