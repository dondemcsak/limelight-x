using Avalonia.Headless.XUnit;
using LimelightX.UI.Components;
using Xunit;

namespace LimelightX.UI.Tests.Edit;

public class CollapsiblePanelTests
{
    [AvaloniaFact]
    public void AccessibleName_ReflectsTitleAndCollapseState()
    {
        var panel = new CollapsiblePanel { Title = "Raw AST", IsCollapsed = false };
        Assert.Equal("Raw AST, expanded", panel.AccessibleName);

        panel.IsCollapsed = true;
        Assert.Equal("Raw AST, collapsed", panel.AccessibleName);
    }

    [AvaloniaFact]
    public void ToggleCommand_FlipsIsCollapsed()
    {
        var panel = new CollapsiblePanel { IsCollapsed = false };

        panel.ToggleCommand.Execute(null);
        Assert.True(panel.IsCollapsed);

        panel.ToggleCommand.Execute(null);
        Assert.False(panel.IsCollapsed);
    }

    [AvaloniaFact]
    public void PanelHeight_DefaultsToNaN_MeaningAutoSize()
    {
        var panel = new CollapsiblePanel();

        Assert.True(double.IsNaN(panel.PanelHeight));
    }

    [AvaloniaFact]
    public void PanelHeight_IsSettable()
    {
        var panel = new CollapsiblePanel { PanelHeight = 220 };

        Assert.Equal(220, panel.PanelHeight);
    }
}
