using System.Reactive;

using Avalonia.Controls;

using DMap.ViewModels;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace DMap.Views;

public partial class StartView : ReactiveUserControl<StartViewModel>
{
    public StartView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            if (ViewModel is null)
                return;

            disposables(ViewModel.ShowSettingsDialog.RegisterHandler(async ctx =>
            {
                var dialog = new SettingsDialog { DataContext = ctx.Input };
                if (TopLevel.GetTopLevel(this) is Window window)
                    await dialog.ShowDialog(window);
                ctx.SetOutput(Unit.Default);
            }));
        });
    }
}
