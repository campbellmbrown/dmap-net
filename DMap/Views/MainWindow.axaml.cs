using DMap.ViewModels;

using ReactiveUI.Avalonia;

namespace DMap.Views;

/// <summary>
/// The application's root window. Binds to <see cref="MainWindowViewModel"/> and displays
/// whatever control is set as <see cref="MainWindowViewModel.Content"/>.
/// </summary>
public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    /// <summary>Initialises the window and loads its XAML layout.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
