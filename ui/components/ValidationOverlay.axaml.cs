using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Components;

/// <summary>
/// Inline error display (ui-components.md §3.3). Renders above CnlEditor
/// per ui-error-handling.md §3 ("Inline errors appear above component
/// content"), and is reused as-is by the inspector panels (Phase 5) since
/// they need the identical treatment for their own Errors collections -
/// EditorViewModel.ValidationErrors is ObservableCollection&lt;ValidationError&gt;
/// while inspector VMs use ObservableCollection&lt;UiError&gt;, so Errors is
/// typed as the common IEnumerable&lt;UiError&gt; (covariant: both collection
/// types satisfy it) rather than picking one subtype.
/// Full squiggly-underline-at-span positioning inside the editor is not yet
/// meaningful: AST/error spans are currently always {0,0} placeholders
/// server-side (confirmed against src/api/dto.rs), so this renders as a
/// readable error list instead of in-text markers.
/// </summary>
public partial class ValidationOverlay : UserControl
{
    public static readonly StyledProperty<IEnumerable<UiError>?> ErrorsProperty =
        AvaloniaProperty.Register<ValidationOverlay, IEnumerable<UiError>?>(nameof(Errors));

    public static readonly DirectProperty<ValidationOverlay, bool> HasErrorsProperty =
        AvaloniaProperty.RegisterDirect<ValidationOverlay, bool>(nameof(HasErrors), o => o.HasErrors);

    private bool _hasErrors;

    public ValidationOverlay()
    {
        InitializeComponent();
    }

    public IEnumerable<UiError>? Errors
    {
        get => GetValue(ErrorsProperty);
        set => SetValue(ErrorsProperty, value);
    }

    public bool HasErrors
    {
        get => _hasErrors;
        private set => SetAndRaise(HasErrorsProperty, ref _hasErrors, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ErrorsProperty)
        {
            return;
        }

        if (change.GetOldValue<IEnumerable<UiError>?>() is INotifyCollectionChanged oldNotifying)
        {
            oldNotifying.CollectionChanged -= OnErrorsCollectionChanged;
        }

        if (change.GetNewValue<IEnumerable<UiError>?>() is INotifyCollectionChanged newNotifying)
        {
            newNotifying.CollectionChanged += OnErrorsCollectionChanged;
        }

        UpdateHasErrors();
    }

    private void OnErrorsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateHasErrors();

    private void UpdateHasErrors()
    {
        HasErrors = Errors is not null && (Errors is ICollection collection ? collection.Count > 0 : Errors.Any());
    }
}
