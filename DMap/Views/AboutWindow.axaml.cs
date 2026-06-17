using Avalonia.Controls;
using Avalonia.Interactivity;

using DMap.ViewModels;

namespace DMap.Views;

/// <summary>Application information dialog showing build version metadata.</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutWindowViewModel();
    }

    void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}
