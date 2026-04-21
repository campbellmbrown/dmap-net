using System;

using Autofac;

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
            var container = BuildContainer();
            var mainVm = container.Resolve<MainWindowViewModel>();
            var navigator = container.Resolve<INavigator>();

            var args = desktop.Args ?? Array.Empty<string>();
            var roleIndex = Array.IndexOf(args, "--role");
            if (roleIndex >= 0 && roleIndex + 1 < args.Length)
            {
                var role = args[roleIndex + 1].ToLowerInvariant();
                if (role == "dm")
                {
                    navigator.NavigateTo(container.Resolve<DmViewModel>());
                }
                else if (role == "player")
                {
                    var playerVm = container.Resolve<PlayerViewModel>();
                    _ = playerVm.StartDiscoveryAsync();
                    navigator.NavigateTo(playerVm);
                }
                else
                {
                    navigator.NavigateTo(container.Resolve<StartViewModel>());
                }
            }
            else
            {
                navigator.NavigateTo(container.Resolve<StartViewModel>());
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<FogMaskService>().As<IFogMaskService>();
        builder.RegisterType<DmHostService>().As<IDmHostService>();
        builder.RegisterType<DiscoveryService>().As<IDiscoveryService>();
        builder.RegisterType<PlayerClientService>().As<IPlayerClientService>();

        builder.RegisterType<MainWindowViewModel>().AsSelf().SingleInstance();
        builder.RegisterType<NavigationService>().As<INavigator>().SingleInstance();
        builder.RegisterType<StartViewModel>().AsSelf();
        builder.RegisterType<DmViewModel>().AsSelf();
        builder.RegisterType<PlayerViewModel>().AsSelf();

        return builder.Build();
    }
}
