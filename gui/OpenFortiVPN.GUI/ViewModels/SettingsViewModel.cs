using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;

namespace OpenFortiVPN.GUI.ViewModels;

/// <summary>
/// ViewModel for the application settings panel.
/// Changes are auto-saved when the user navigates away or clicks Save.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    // --- Startup ---

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _autoConnectLastProfile;

    // --- UI ---

    [ObservableProperty]
    private ThemeMode _theme;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private bool _showNotifications = true;

    [ObservableProperty]
    private bool _showConnectionDuration = true;

    // --- Logging ---

    [ObservableProperty]
    private VpnLogLevel _logVerbosity = VpnLogLevel.Info;

    [ObservableProperty]
    private int _maxLogRetentionDays = 30;

    // --- CLI ---

    [ObservableProperty]
    private string _openFortiVpnPath = "openfortivpn.exe";

    [ObservableProperty]
    private string? _statusMessage;

    // Enum values for combo boxes
    public ThemeMode[] ThemeModes { get; } = Enum.GetValues<ThemeMode>();
    public VpnLogLevel[] LogLevels { get; } = Enum.GetValues<VpnLogLevel>();

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Current;
        StartWithWindows = s.StartWithWindows;
        StartMinimized = s.StartMinimized;
        AutoConnectLastProfile = s.AutoConnectLastProfile;
        Theme = s.Theme;
        MinimizeToTray = s.MinimizeToTray;
        CloseToTray = s.CloseToTray;
        ShowNotifications = s.ShowNotifications;
        ShowConnectionDuration = s.ShowConnectionDuration;
        LogVerbosity = s.LogVerbosity;
        MaxLogRetentionDays = s.MaxLogRetentionDays;
        OpenFortiVpnPath = s.OpenFortiVpnPath;
    }

    [RelayCommand]
    private async Task Save()
    {
        var s = _settingsService.Current;
        s.StartWithWindows = StartWithWindows;
        s.StartMinimized = StartMinimized;
        s.AutoConnectLastProfile = AutoConnectLastProfile;
        s.Theme = Theme;
        s.MinimizeToTray = MinimizeToTray;
        s.CloseToTray = CloseToTray;
        s.ShowNotifications = ShowNotifications;
        s.ShowConnectionDuration = ShowConnectionDuration;
        s.LogVerbosity = LogVerbosity;
        s.MaxLogRetentionDays = MaxLogRetentionDays;
        s.OpenFortiVpnPath = OpenFortiVpnPath;

        await _settingsService.SaveAsync();
        StatusMessage = "Settings saved.";

        // Apply startup registration
        UpdateWindowsStartup(StartWithWindows);
    }

    [RelayCommand]
    private void BrowseForCli()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Locate openfortivpn.exe",
            Filter = "Executable (openfortivpn.exe)|openfortivpn.exe|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            OpenFortiVpnPath = dlg.FileName;
        }
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var defaults = new AppSettings();
        StartWithWindows = defaults.StartWithWindows;
        StartMinimized = defaults.StartMinimized;
        AutoConnectLastProfile = defaults.AutoConnectLastProfile;
        Theme = defaults.Theme;
        MinimizeToTray = defaults.MinimizeToTray;
        CloseToTray = defaults.CloseToTray;
        ShowNotifications = defaults.ShowNotifications;
        ShowConnectionDuration = defaults.ShowConnectionDuration;
        LogVerbosity = defaults.LogVerbosity;
        MaxLogRetentionDays = defaults.MaxLogRetentionDays;
        OpenFortiVpnPath = defaults.OpenFortiVpnPath;
        StatusMessage = "Reset to defaults. Click Save to apply.";
    }

    private static void UpdateWindowsStartup(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);

            if (key is null) return;

            const string appName = "OpenFortiVPN";
            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath is not null)
                    key.SetValue(appName, $"\"{exePath}\" --minimized");
            }
            else
            {
                key.DeleteValue(appName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Non-critical — user may not have registry access
        }
    }
}
