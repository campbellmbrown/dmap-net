using DMap.ViewModels;

using ReactiveUI.Avalonia;

namespace DMap.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
