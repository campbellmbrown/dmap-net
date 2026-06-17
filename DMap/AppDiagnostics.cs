using System;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Threading;

using Serilog;

namespace DMap;

/// <summary>
/// Centralises application log-file configuration and fatal crash logging.
/// </summary>
internal static class AppDiagnostics
{
    /// <summary>The shared per-user diagnostics directory.</summary>
    public static string LogDirectory { get; } = ResolveLogDirectory();

    /// <summary>Configures the global Serilog logger to write one log file per app run.</summary>
    public static void ConfigureLogging()
    {
        Directory.CreateDirectory(LogDirectory);

        var logFilePath = Path.Combine(
            ResolveLogDirectory(),
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                logFilePath,
                outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>Registers process-wide exception hooks.</summary>
    public static void RegisterProcessExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
    }

    /// <summary>Registers the Avalonia UI dispatcher exception hook.</summary>
    public static void RegisterUiExceptionHandler()
    {
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
    }

    /// <summary>
    /// Logs a fatal exception to the current session log and flushes it immediately.
    /// </summary>
    public static void ReportFatal(string source, Exception exception, bool isTerminating)
    {
        Log.Fatal(
            exception,
            "Unhandled exception observed via {Source}. Terminating: {IsTerminating}",
            source,
            isTerminating);
        Log.CloseAndFlush();
    }

    static void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception
            ?? new InvalidOperationException(
                $"Non-exception unhandled failure: {e.ExceptionObject}");

        ReportFatal("AppDomain.CurrentDomain.UnhandledException", exception, e.IsTerminating);
    }

    static void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ReportFatal("TaskScheduler.UnobservedTaskException", e.Exception, isTerminating: false);
    }

    static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ReportFatal("Dispatcher.UIThread.UnhandledException", e.Exception, isTerminating: true);
    }

    static string ResolveLogDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "DMap", "logs");
    }
}
