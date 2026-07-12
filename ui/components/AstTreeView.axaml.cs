using Avalonia;
using Avalonia.Controls;
using LimelightX.UI.ViewModels.Inspectors;

namespace LimelightX.UI.Components;

public partial class AstTreeView : UserControl
{
    public static readonly StyledProperty<AstNodeViewModel?> RootNodeProperty =
        AvaloniaProperty.Register<AstTreeView, AstNodeViewModel?>(nameof(RootNode));

    public static readonly DirectProperty<AstTreeView, IReadOnlyList<AstNodeViewModel>> RootItemsProperty =
        AvaloniaProperty.RegisterDirect<AstTreeView, IReadOnlyList<AstNodeViewModel>>(nameof(RootItems), o => o.RootItems);

    private IReadOnlyList<AstNodeViewModel> _rootItems = [];

    public AstTreeView()
    {
        InitializeComponent();
    }

    public AstNodeViewModel? RootNode
    {
        get => GetValue(RootNodeProperty);
        set => SetValue(RootNodeProperty, value);
    }

    public IReadOnlyList<AstNodeViewModel> RootItems
    {
        get => _rootItems;
        private set => SetAndRaise(RootItemsProperty, ref _rootItems, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RootNodeProperty)
        {
            var root = change.GetNewValue<AstNodeViewModel?>();
            RootItems = root is null ? [] : [root];
        }
    }
}
