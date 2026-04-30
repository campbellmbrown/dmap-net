using System.Reactive;

using Avalonia.Controls;

using ReactiveUI;

namespace DMap.ViewModels;

/// <summary>
/// ViewModel for the application's main window.
/// Hosts the active top-level view (currently always the DM view) as a swappable
/// <see cref="Content"/> control so the window shell can remain independent of
/// which view is displayed.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// The control currently displayed inside the main window.
    /// Setting this property triggers a UI update via <see cref="ReactiveUI.IReactiveObject"/>.
    /// </summary>
    public Control? Content
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public WindowState WindowState
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = WindowState.Maximized;

    public ReactiveCommand<Unit, Unit> ToggleFullScreenCommand { get; }

    public MainWindowViewModel()
    {
        ToggleFullScreenCommand = ReactiveCommand.Create(ToggleFullScreen);
    }

    void ToggleFullScreen()
    {
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
    }
}
