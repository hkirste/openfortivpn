using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;

namespace OpenFortiVPN.GUI.ViewModels;

/// <summary>
/// Root ViewModel for the main window. Owns the navigation sidebar,
/// system tray integration, and global connection state display.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly IVpnService _vpnService;
    private readonly ISettingsService _settingsService;
    private readonly IDispatcherService _dispatcher;

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private string _windowTitle = "OpenFortiVPN";

    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    [ObservableProperty]
    private string _statusText = "Disconnected";

    [ObservableProperty]
    private string _trayTooltip = "OpenFortiVPN - Disconnected";

    [ObservableProperty]
    private bool _isWindowVisible = true;

    [ObservableProperty]
    private string _selectedNavItem = "Dashboard";

    public MainViewModel(
        INavigationService navigation,
        IVpnService vpnService,
        ISettingsService settingsService,
        IDispatcherService dispatcher)
    {
        _navigation = navigation;
        _vpnService = vpnService;
        _settingsService = settingsService;
        _dispatcher = dispatcher;

        // Subscribe to navigation changes
        _navigation.Navigated += (_, vm) => CurrentView = vm;

        // Subscribe to VPN state changes
        _vpnService.StateChanged += OnVpnStateChanged;

        // Start on dashboard
        _navigation.NavigateTo<DashboardViewModel>();
    }

    private void OnVpnStateChanged(object? sender, ConnectionState newState)
    {
        _dispatcher.Invoke(() =>
        {
            ConnectionState = newState;
            StatusText = newState switch
            {
                ConnectionState.Disconnected => "Disconnected",
                ConnectionState.Connecting => "Connecting...",
                ConnectionState.Authenticating => "Authenticating...",
                ConnectionState.WaitingForOtp => "Waiting for OTP...",
                ConnectionState.WaitingForSaml => "Waiting for SAML login...",
                ConnectionState.NegotiatingTunnel => "Setting up tunnel...",
                ConnectionState.Connected => "Connected",
                ConnectionState.Reconnecting => "Reconnecting...",
                ConnectionState.Disconnecting => "Disconnecting...",
                ConnectionState.Error => "Error",
                _ => "Unknown"
            };
            TrayTooltip = $"OpenFortiVPN - {StatusText}";
        });
    }

    [RelayCommand]
    private void NavigateTo(string viewName)
    {
        SelectedNavItem = viewName;
        switch (viewName)
        {
            case "Dashboard":
                _navigation.NavigateTo<DashboardViewModel>();
                break;
            case "Profiles":
                _navigation.NavigateTo<ProfileListViewModel>();
                break;
            case "Logs":
                _navigation.NavigateTo<LogViewerViewModel>();
                break;
            case "Settings":
                _navigation.NavigateTo<SettingsViewModel>();
                break;
        }
    }

    [RelayCommand]
    private void ShowWindow()
    {
        IsWindowVisible = true;
    }

    [RelayCommand]
    private void HideWindow()
    {
        if (_settingsService.Current.MinimizeToTray)
        {
            IsWindowVisible = false;
        }
    }

    [RelayCommand]
    private async Task QuickDisconnect()
    {
        if (_vpnService.IsActive)
        {
            await _vpnService.DisconnectAsync();
        }
    }
}
