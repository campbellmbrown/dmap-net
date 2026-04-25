using System;

using Avalonia;

using ReactiveUI.Avalonia;

namespace DMap;

/// <summary>
/// Application entry point. Must run on an STA thread (required by some platform UI frameworks).
/// </summary>
sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    /// <summary>
    /// Application entry point. Builds the Avalonia app and starts the classic desktop lifetime.
    /// </summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    /// <summary>
    /// Configures the Avalonia <see cref="AppBuilder"/> with platform detection, the Inter font,
    /// trace logging, and ReactiveUI integration.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(_ => { });
}
