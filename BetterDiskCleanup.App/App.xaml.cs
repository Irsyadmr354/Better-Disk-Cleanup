using BetterDiskCleanup.App.ViewModels;
using BetterDiskCleanup.Core.Recovery;
using BetterDiskCleanup.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Json;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace BetterDiskCleanup.App;

public partial class App : Application
{
    private IHost? _host;

    /// <summary>
    /// Fallback crash log path, written BEFORE Serilog is initialised so that
    /// catastrophic startup failures always leave a trace on disk.
    /// </summary>
    private static readonly string FallbackCrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BetterDiskCleanup",
        "logs",
        "crash-fallback.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        // ── Global exception handlers (installed as early as possible) ──────
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BetterDiskCleanup",
                "logs");

            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(new JsonFormatter())
                .WriteTo.File(
                    new JsonFormatter(),
                    Path.Combine(logDirectory, "app-.log"),
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // Ensure the recovery staging root exists BEFORE any DI resolution
            // touches RecoverySnapshotService (whose constructor assumes the
            // directory is present).  This makes startup resilient to the
            // staging folder being deleted externally.
            EnsureRecoveryStagingDirectoryExists();

            // Resolve appsettings.json relative to the assembly output
            // directory so the app works regardless of the process working
            // directory (e.g. `dotnet run` from the solution root).
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<RecoveryOptions>(
                        context.Configuration.GetSection(RecoveryOptions.SectionName));
                    services.AddBetterDiskCleanupInfrastructure();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<RecoveryHistoryViewModel>();
                    services.AddTransient<BrowserCleanupViewModel>();
                    services.AddTransient<LargeFileFinderViewModel>();
                    services.AddTransient<MainShellViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            _host.StartAsync().GetAwaiter().GetResult();

            _host.Services
                .GetRequiredService<ILogger<App>>()
                .LogInformation("Better Disk Cleanup application started.");

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            WriteFallbackCrashLog(ex);

            // Try to log through Serilog as well (it may already be initialised).
            try
            {
                Log.Fatal(ex, "Fatal error during application startup.");
            }
            catch
            {
                // Serilog not available — fallback log is already written.
            }

            MessageBox.Show(
                $"Better Disk Cleanup failed to start.\n\n{ex.Message}\n\n" +
                $"Details written to:\n{FallbackCrashLogPath}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }

    // ── Global exception handler implementations ────────────────────────────

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception ?? new Exception("Unknown non-Exception object thrown.");
        WriteFallbackCrashLog(ex, context: e.IsTerminating ? "AppDomain (terminating)" : "AppDomain (non-terminating)");

        try
        {
            Log.Fatal(ex, "AppDomain.UnhandledException (IsTerminating={IsTerminating})", e.IsTerminating);
        }
        catch { /* fallback already written */ }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteFallbackCrashLog(e.Exception, context: "DispatcherUnhandled");

        try
        {
            Log.Error(e.Exception, "DispatcherUnhandledException on UI thread.");
        }
        catch { /* fallback already written */ }

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n" +
            $"Details written to:\n{FallbackCrashLogPath}",
            "Unexpected Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteFallbackCrashLog(e.Exception, context: "UnobservedTaskException");

        try
        {
            Log.Error(e.Exception, "UnobservedTaskException.");
        }
        catch { /* fallback already written */ }

        e.SetObserved();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the recovery staging root directory if it does not exist.
    /// This runs synchronously BEFORE the DI host is built, so that
    /// <see cref="Infrastructure.Recovery.RecoverySnapshotService"/> constructor
    /// never encounters a missing parent directory.
    /// </summary>
    private static void EnsureRecoveryStagingDirectoryExists()
    {
        try
        {
            var userTempRoot = Path.GetTempPath()
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var stagingRoot = Path.Combine(userTempRoot, "BetterDiskCleanup", "Recovery");
            Directory.CreateDirectory(stagingRoot);

            Log.Information("Recovery staging directory ensured: {Path}", stagingRoot);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not pre-create recovery staging directory. " +
                            "RecoverySnapshotService will attempt to create it on first use.");
        }
    }

    /// <summary>
    /// Writes exception details to a plain-text file that does NOT depend on
    /// Serilog.  Guarantees that crash information is always persisted.
    /// </summary>
    private static void WriteFallbackCrashLog(Exception ex, string? context = null)
    {
        try
        {
            var directory = Path.GetDirectoryName(FallbackCrashLogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entry = $"[{DateTimeOffset.UtcNow:O}]" +
                        (context is not null ? $" [{context}]" : string.Empty) +
                        $"{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";

            File.AppendAllText(FallbackCrashLogPath, entry);
        }
        catch
        {
            // Absolute last resort — nothing more we can do.
        }
    }
}
