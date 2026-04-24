using Autofac;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using DMap.Services.Fog;
using DMap.Services.History;
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

            navigator.NavigateTo(container.Resolve<DmViewModel>());

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
        builder.RegisterType<UndoRedoService>().As<IUndoRedoService>().SingleInstance();
        builder.RegisterType<DmHostService>().As<IDmHostService>();
        builder.RegisterType<DiscoveryService>().As<IDiscoveryService>();
        builder.RegisterType<PlayerClientService>().As<IPlayerClientService>();

        builder.RegisterType<MainWindowViewModel>().AsSelf().SingleInstance();
        builder.RegisterType<NavigationService>().As<INavigator>().SingleInstance();
        builder.RegisterType<DmViewModel>().AsSelf();
        builder.RegisterType<PlayerViewModel>().AsSelf();

        return builder.Build();
    }
}
