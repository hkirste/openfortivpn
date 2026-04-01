using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using OpenFortiVPN.GUI.Services;
using OpenFortiVPN.GUI.ViewModels;
using OpenFortiVPN.GUI.Views;

namespace OpenFortiVPN.GUI;

/// <summary>
/// Application entry point with full dependency injection setup.
///
/// Architecture overview:
/// - Services are registered as singletons (single VPN connection, single settings instance)
/// - ViewModels are transient (new instance per navigation)
/// - Views resolve their ViewModel from the container via DataContext
/// - Serilog handles structured logging to rolling files
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure Serilog
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenFortiVPN", "logs", "app-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Load settings and profiles
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        var profileService = _serviceProvider.GetRequiredService<IProfileService>();
        await profileService.LoadAsync();

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

        // Handle --minimized startup argument
        if (e.Args.Contains("--minimized") && settingsService.Current.StartMinimized)
        {
            mainWindow.WindowState = WindowState.Minimized;
            mainWindow.ShowInTaskbar = false;
        }
        else
        {
            mainWindow.Show();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        // Services (singletons — shared state)
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<ICredentialService, CredentialService>();
        services.AddSingleton<IVpnService, VpnService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDispatcherService, WpfDispatcherService>();

        // ViewModels (transient — new per navigation)
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProfileListViewModel>();
        services.AddTransient<ProfileEditorViewModel>();
        services.AddTransient<LogViewerViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Ensure VPN is disconnected on exit
        if (_serviceProvider?.GetService<IVpnService>() is VpnService vpnService)
        {
            vpnService.Dispose();
        }

        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
