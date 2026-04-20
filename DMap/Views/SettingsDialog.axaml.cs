using Avalonia.Controls;

namespace DMap.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
        this.FindControl<Button>("CloseButton")!.Click += (_, _) => Close();
    }
}
