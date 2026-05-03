using System;
using System.Windows.Input;

namespace DMap.Commands;

/// <summary>
/// Adapts a parameterless <see cref="Action"/> to <see cref="ICommand"/> so XAML controls can invoke local view/control behavior.
/// </summary>
public sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
