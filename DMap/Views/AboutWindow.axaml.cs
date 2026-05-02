using System.Reflection;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DMap.Views;

/// <summary>Application information dialog showing build version metadata.</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        InformationalVersionText.Text = GetInformationalVersion();
    }

    static string GetInformationalVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AboutWindow).Assembly;
        return assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "Unknown";
    }

    void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}
