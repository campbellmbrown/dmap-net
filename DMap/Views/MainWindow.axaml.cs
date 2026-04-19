using ReactiveUI.Avalonia;
using DMap.ViewModels;

namespace DMap.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();
    }
}