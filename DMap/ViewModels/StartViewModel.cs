using System;
using System.Reactive;

using ReactiveUI;

namespace DMap.ViewModels;

public class StartViewModel : ViewModelBase
{
    readonly INavigator _navigator;
    readonly Func<DmViewModel> _createDm;
    readonly Func<PlayerViewModel> _createPlayer;

    public ReactiveCommand<Unit, Unit> StartAsDmCommand { get; }
    public ReactiveCommand<Unit, Unit> StartAsPlayerCommand { get; }

    public StartViewModel(INavigator navigator, Func<DmViewModel> createDm, Func<PlayerViewModel> createPlayer)
    {
        _navigator = navigator;
        _createDm = createDm;
        _createPlayer = createPlayer;
        StartAsDmCommand = ReactiveCommand.Create(StartAsDm);
        StartAsPlayerCommand = ReactiveCommand.Create(StartAsPlayer);
    }

    void StartAsDm()
    {
        _navigator.NavigateTo(_createDm());
    }

    void StartAsPlayer()
    {
        var vm = _createPlayer();
        _ = vm.StartDiscoveryAsync();
        _navigator.NavigateTo(vm);
    }
}
