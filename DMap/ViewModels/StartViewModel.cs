using System.Reactive;

using DMap.Services.Brushes;
using DMap.Services.Fog;
using DMap.Services.Networking;

using ReactiveUI;

namespace DMap.ViewModels;

public class StartViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _host;

    public ReactiveCommand<Unit, Unit> StartAsDmCommand { get; }
    public ReactiveCommand<Unit, Unit> StartAsPlayerCommand { get; }

    public StartViewModel(MainWindowViewModel host)
    {
        _host = host;
        StartAsDmCommand = ReactiveCommand.Create(StartAsDm);
        StartAsPlayerCommand = ReactiveCommand.Create(StartAsPlayer);
    }

    private void StartAsDm()
    {
        var fogService = new FogMaskService();
        var brush = new CircleBrush();
        var hostService = new DmHostService();
        var discoveryService = new DiscoveryService();

        var vm = new DmViewModel(fogService, brush);
        vm.InitializeNetworking(hostService, discoveryService);

        _host.NavigateTo(vm);
    }

    private void StartAsPlayer()
    {
        var fogService = new FogMaskService();
        var discoveryService = new DiscoveryService();
        var clientService = new PlayerClientService();

        var vm = new PlayerViewModel(fogService);
        vm.InitializeNetworking(discoveryService, clientService);
        _ = vm.StartDiscoveryAsync();

        _host.NavigateTo(vm);
    }
}
