using System;
using System.Diagnostics;

using Avalonia.Controls;

using DMap.ViewModels;
using DMap.Views;

namespace DMap;

internal sealed class NavigationService : INavigator
{
    private readonly MainWindowViewModel _mainVm;

    public NavigationService(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;
    }

    public void NavigateTo(ViewModelBase vm)
    {
        if (_mainVm.Content is IDisposable disposable)
            disposable.Dispose();

        _mainVm.Content = vm switch
        {
            StartViewModel m  => new StartView { DataContext = m },
            DmViewModel m     => new DmView { DataContext = m },
            PlayerViewModel m => new PlayerView { DataContext = m },
            _                 => throw new UnreachableException(),
        };
    }
}
