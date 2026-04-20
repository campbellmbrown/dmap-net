using System.Reactive;

using DMap.ViewModels;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace DMap.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            if (ViewModel is null)
                return;

            disposables(ViewModel.ShowSettingsDialog.RegisterHandler(async ctx =>
            {
                var dialog = new SettingsDialog { DataContext = ctx.Input };
                await dialog.ShowDialog(this);
                ctx.SetOutput(Unit.Default);
            }));
        });
    }
}
