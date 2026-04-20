using DMap.ViewModels;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace DMap.Views;

public partial class SettingsDialog : ReactiveWindow<SettingsViewModel>
{
    public SettingsDialog()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            if (ViewModel is null)
                return;

            disposables(ViewModel.CloseCommand.Subscribe(_ => Close()));
        });
    }
}
