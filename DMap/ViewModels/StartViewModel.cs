using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

using ReactiveUI;

namespace DMap.ViewModels;

public class StartViewModel : ViewModelBase
{
    private readonly INavigator _navigator;
    private readonly Func<DmViewModel> _createDm;
    private readonly Func<PlayerViewModel> _createPlayer;
    private readonly Func<SettingsViewModel> _createSettings;

    public ReactiveCommand<Unit, Unit> StartAsDmCommand { get; }
    public ReactiveCommand<Unit, Unit> StartAsPlayerCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }

    public Interaction<SettingsViewModel, Unit> ShowSettingsDialog { get; } = new();

    public StartViewModel(
        INavigator navigator,
        Func<DmViewModel> createDm,
        Func<PlayerViewModel> createPlayer,
        Func<SettingsViewModel> createSettings)
    {
        _navigator = navigator;
        _createDm = createDm;
        _createPlayer = createPlayer;
        _createSettings = createSettings;

        StartAsDmCommand = ReactiveCommand.Create(StartAsDm);
        StartAsPlayerCommand = ReactiveCommand.Create(StartAsPlayer);
        OpenSettingsCommand = ReactiveCommand.CreateFromTask(OpenSettingsAsync);
    }

    private async Task OpenSettingsAsync()
    {
        await ShowSettingsDialog.Handle(_createSettings());
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
