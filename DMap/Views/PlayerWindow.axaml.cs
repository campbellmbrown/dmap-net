using Avalonia.Controls;

namespace DMap.Views;

/// <summary>
/// Top-level player window shell hosting <see cref="PlayerView"/>.
/// Window chrome and bindings live in XAML so the DM view doesn't construct them in code.
/// </summary>
public partial class PlayerWindow : Window
{
    public PlayerWindow()
    {
        InitializeComponent();
    }
}
