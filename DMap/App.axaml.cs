using System;

using Autofac;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using DMap.Services.Brushes;
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

            var args = desktop.Args ?? Array.Empty<string>();
            var roleIndex = Array.IndexOf(args, "--role");
            if (roleIndex >= 0 && roleIndex + 1 < args.Length)
            {
                var role = args[roleIndex + 1].ToLowerInvariant();
                if (role == "dm")
                {
                    mainVm.NavigateTo(container.Resolve<DmViewModel>());
                }
                else if (role == "player")
                {
                    var playerVm = container.Resolve<PlayerViewModel>();
                    _ = playerVm.StartDiscoveryAsync();
                    mainVm.NavigateTo(playerVm);
                }
                else
                {
                    mainVm.NavigateTo(container.Resolve<StartViewModel>());
                }
            }
            else
            {
                mainVm.NavigateTo(container.Resolve<StartViewModel>());
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<FogMaskService>().As<IFogMaskService>();
        builder.RegisterType<CircleBrush>().As<IBrush>();
        builder.RegisterType<DmHostService>().As<IDmHostService>();
        builder.RegisterType<DiscoveryService>().As<IDiscoveryService>();
        builder.RegisterType<PlayerClientService>().As<IPlayerClientService>();

        builder.RegisterType<MainWindowViewModel>().AsSelf().As<INavigator>().SingleInstance();
        builder.RegisterType<StartViewModel>().AsSelf();
        builder.RegisterType<DmViewModel>().AsSelf();
        builder.RegisterType<PlayerViewModel>().AsSelf();

        return builder.Build();
    }
}
