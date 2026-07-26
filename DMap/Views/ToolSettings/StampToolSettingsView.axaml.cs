using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace DMap.Views.ToolSettings;

/// <summary>
/// Stamp tool settings panel.
/// </summary>
public partial class StampToolSettingsView : UserControl
{
    public StampToolSettingsView()
    {
        InitializeComponent();
    }

    void StampCatalogPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>() is null)
            return;

        StampCatalogButton.Flyout?.Hide();
    }
}
