using System.ComponentModel;

namespace LimelightX.UI.ViewModels.Inspectors;

/// <summary>
/// Shared shape for the six inspector panels' resize/collapse state
/// (ui-viewmodels.md §11) - lets CnlTabView's code-behind wire the
/// accordion Grid-row &lt;-&gt; ViewModel two-way sync generically instead of
/// repeating it once per panel.
/// </summary>
public interface IResizablePanelViewModel : INotifyPropertyChanged
{
    bool IsCollapsed { get; set; }

    double Height { get; set; }
}
