using System;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using DMap.Services.Fog;
using DMap.Services.Networking;
using DMap.ViewModels;
using DMap.Views;

namespace DMap;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };

            var args = desktop.Args ?? Array.Empty<string>();
            var roleIndex = Array.IndexOf(args, "--role");
            if (roleIndex >= 0 && roleIndex + 1 < args.Length)
            {
                var role = args[roleIndex + 1].ToLowerInvariant();
                if (role == "dm")
                {
                    var fogService = new FogMaskService();
                    var hostService = new DmHostService();
                    var discoveryService = new DiscoveryService();
                    var vm = new DmViewModel(fogService);
                    vm.InitializeNetworking(hostService, discoveryService);
                    mainVm.NavigateTo(vm);
                }
                else if (role == "player")
                {
                    var fogService = new FogMaskService();
                    var discoveryService = new DiscoveryService();
                    var clientService = new PlayerClientService();
                    var vm = new PlayerViewModel(fogService);
                    vm.InitializeNetworking(discoveryService, clientService);
                    _ = vm.StartDiscoveryAsync();
                    mainVm.NavigateTo(vm);
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
