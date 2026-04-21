using System;

using DMap.ViewModels;
using DMap.Views;

namespace DMap;

internal sealed class NavigationService(MainWindowViewModel mainVm) : INavigator
{
    public void NavigateTo(ViewModelBase vm)
    {
        if (mainVm.Content?.DataContext is IDisposable disposable)
            disposable.Dispose();

        mainVm.Content = vm switch
        {
            StartViewModel m => new StartView { DataContext = m },
            DmViewModel m => new DmView { DataContext = m },
            PlayerViewModel m => new PlayerView { DataContext = m },
            _ => throw new ArgumentOutOfRangeException(nameof(vm), vm.GetType().Name, "No view registered for ViewModel"),
        };
    }
}
