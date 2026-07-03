using Avalonia;
using Avalonia.Controls;
using LimelightX.UI.Services.Dto;

namespace LimelightX.UI.Components;

public partial class AstTreeView : UserControl
{
    public static readonly StyledProperty<AstNode?> TreeProperty =
        AvaloniaProperty.Register<AstTreeView, AstNode?>(nameof(Tree));

    public static readonly DirectProperty<AstTreeView, IReadOnlyList<AstNode>> RootItemsProperty =
        AvaloniaProperty.RegisterDirect<AstTreeView, IReadOnlyList<AstNode>>(nameof(RootItems), o => o.RootItems);

    private IReadOnlyList<AstNode> _rootItems = [];

    public AstTreeView()
    {
        InitializeComponent();
    }

    public AstNode? Tree
    {
        get => GetValue(TreeProperty);
        set => SetValue(TreeProperty, value);
    }

    public IReadOnlyList<AstNode> RootItems
    {
        get => _rootItems;
        private set => SetAndRaise(RootItemsProperty, ref _rootItems, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TreeProperty)
        {
            var tree = change.GetNewValue<AstNode?>();
            RootItems = tree is null ? [] : [tree];
        }
    }
}
