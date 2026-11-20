using Avalonia.Controls;
using Avalonia.Interactivity;

using DMap.ViewModels;

namespace DMap.Views;

/// <summary>
/// Modal dialog asking whether to save, discard or keep unsaved project changes.
/// Closing the window without choosing returns <see cref="UnsavedChangesChoice.Cancel"/>.
/// </summary>
public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    /// <summary>Creates the dialog for the named project.</summary>
    public UnsavedChangesDialog(string projectName) : this()
    {
        this.FindControl<TextBlock>("MessageText")!.Text = $"Do you want to save changes to {projectName}?";
    }

    void SaveButton_Click(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Save);

    void DiscardButton_Click(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Discard);

    void CancelButton_Click(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Cancel);
}
