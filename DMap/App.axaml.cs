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

/// <summary>
/// Root Avalonia application class. Initialises the dependency injection container and
/// wires up the main window with its ViewModel on framework startup.
/// </summary>
public partial class App : Application
{
    /// <summary>Loads the application XAML resources.</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Builds the DI container, resolves the root ViewModels, and creates the main window
    /// containing the DM view.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var container = BuildContainer();
            var mainVm = container.Resolve<MainWindowViewModel>();
            var dmVm = container.Resolve<DmViewModel>();
            mainVm.Content = new DmView { DataContext = dmVm };

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Registers all service and ViewModel types with Autofac.
    /// <list type="bullet">
    ///   <item><see cref="FogMaskService"/> — transient fog mask (one per DM/player context).</item>
    ///   <item><see cref="UndoRedoService"/> — singleton undo/redo history shared by the DM.</item>
    ///   <item><see cref="DmHostService"/> — TCP server for broadcasting to players.</item>
    ///   <item><see cref="DiscoveryService"/> — UDP broadcast/listen for session discovery.</item>
    ///   <item><see cref="PlayerClientService"/> — TCP client for connecting to the DM.</item>
    /// </list>
    /// </summary>
    static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();

        builder.RegisterType<FogMaskService>().As<IFogMaskService>();
        builder.RegisterType<UndoRedoService>().As<IUndoRedoService>().SingleInstance();
        builder.RegisterType<DmHostService>().As<IDmHostService>();
        builder.RegisterType<DiscoveryService>().As<IDiscoveryService>();
        builder.RegisterType<PlayerClientService>().As<IPlayerClientService>();

        builder.RegisterType<MainWindowViewModel>().AsSelf().SingleInstance();
        builder.RegisterType<DmViewModel>().AsSelf();
        builder.RegisterType<PlayerViewModel>().AsSelf();

        return builder.Build();
    }
}
