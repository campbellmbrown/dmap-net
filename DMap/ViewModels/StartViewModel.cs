using System;
using System.Reactive;

using ReactiveUI;

namespace DMap.ViewModels;

public class StartViewModel : ViewModelBase
{
    private readonly INavigator _navigator;
    private readonly Func<DmViewModel> _createDm;
    private readonly Func<PlayerViewModel> _createPlayer;

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

    private void StartAsDm()
    {
        _navigator.NavigateTo(_createDm());
    }

    private void StartAsPlayer()
    {
        var vm = _createPlayer();
        _ = vm.StartDiscoveryAsync();
        _navigator.NavigateTo(vm);
    }
}
