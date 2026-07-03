using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using LimelightX.UI.ViewModels.Errors;

namespace LimelightX.UI.Components;

public partial class ErrorBanner : UserControl
{
    public static readonly StyledProperty<IEnumerable<UiError>?> ErrorsProperty =
        AvaloniaProperty.Register<ErrorBanner, IEnumerable<UiError>?>(nameof(Errors));

    public static readonly DirectProperty<ErrorBanner, bool> HasErrorsProperty =
        AvaloniaProperty.RegisterDirect<ErrorBanner, bool>(nameof(HasErrors), o => o.HasErrors);

    private bool _hasErrors;

    public ErrorBanner()
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
