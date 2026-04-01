using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;

namespace OpenFortiVPN.GUI.ViewModels;

/// <summary>
/// Dashboard ViewModel — the home screen showing connection status,
/// quick-connect controls, and recent activity.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IVpnService _vpnService;
    private readonly IProfileService _profileService;
    private readonly ICredentialService _credentialService;
    private readonly INavigationService _navigation;
    private readonly INotificationService _notifications;
    private readonly DispatcherTimer _durationTimer;

    // --- Connection State ---

    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    [ObservableProperty]
    private string _statusText = "Ready to connect";

    [ObservableProperty]
    private string _connectionDuration = "00:00:00";

    [ObservableProperty]
    private string? _assignedIp;

    [ObservableProperty]
    private string? _gatewayIp;

    [ObservableProperty]
    private string? _dnsInfo;

    [ObservableProperty]
    private string? _tunnelInterface;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _errorSuggestion;

    // --- Quick Connect ---

    [ObservableProperty]
    private VpnProfile? _selectedProfile;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _savePassword;

    [ObservableProperty]
    private bool _hasProfiles;

    [ObservableProperty]
    private string? _otpToken;

    [ObservableProperty]
    private bool _isOtpRequired;

    // --- Recent Logs ---

    public ObservableCollection<LogEntry> RecentLogs { get; } = new();

    public ObservableCollection<VpnProfile> Profiles { get; } = new();

    public DashboardViewModel(
        IVpnService vpnService,
        IProfileService profileService,
        ICredentialService credentialService,
        INavigationService navigation,
        INotificationService notifications)
    {
        _vpnService = vpnService;
        _profileService = profileService;
        _credentialService = credentialService;
        _navigation = navigation;
        _notifications = notifications;

        // Duration timer
        _durationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _durationTimer.Tick += (_, _) => UpdateDuration();

        // Wire up events
        _vpnService.StateChanged += OnStateChanged;
        _vpnService.LogReceived += OnLogReceived;
        _vpnService.OtpRequired += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsOtpRequired = true;
                StatusText = "Enter your one-time password (OTP)";
            });
        };

        LoadProfiles();
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        foreach (var p in _profileService.Profiles)
            Profiles.Add(p);

        HasProfiles = Profiles.Count > 0;

        if (Profiles.Count > 0)
            SelectedProfile = Profiles[0];
    }

    partial void OnSelectedProfileChanged(VpnProfile? value)
    {
        if (value is not null && _credentialService.HasPassword(value.Id))
        {
            Password = _credentialService.LoadPassword(value.Id) ?? string.Empty;
            SavePassword = true;
        }
        else
        {
            Password = string.Empty;
            SavePassword = false;
        }
    }

    [RelayCommand]
    private async Task Connect()
    {
        if (SelectedProfile is null) return;

        HasError = false;
        ErrorMessage = null;

        // Save password if requested
        if (SavePassword && !string.IsNullOrEmpty(Password))
        {
            _credentialService.SavePassword(SelectedProfile.Id, Password);
        }

        try
        {
            await _vpnService.ConnectAsync(SelectedProfile, Password);
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        await _vpnService.DisconnectAsync();
    }

    [RelayCommand]
    private async Task SubmitOtp()
    {
        if (!string.IsNullOrWhiteSpace(OtpToken))
        {
            await _vpnService.SubmitOtpAsync(OtpToken);
            IsOtpRequired = false;
            OtpToken = null;
        }
    }

    [RelayCommand]
    private void CreateNewProfile()
    {
        _navigation.NavigateTo<ProfileEditorViewModel>();
    }

    [RelayCommand]
    private void DismissError()
    {
        HasError = false;
        ErrorMessage = null;
    }

    private void OnStateChanged(object? sender, ConnectionState newState)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ConnectionState = newState;
            IsConnected = newState == ConnectionState.Connected;
            IsConnecting = newState is ConnectionState.Connecting
                or ConnectionState.Authenticating
                or ConnectionState.NegotiatingTunnel
                or ConnectionState.Reconnecting;
            HasError = newState == ConnectionState.Error;

            var conn = _vpnService.CurrentConnection;

            StatusText = newState switch
            {
                ConnectionState.Disconnected => "Ready to connect",
                ConnectionState.Connecting => "Connecting to gateway...",
                ConnectionState.Authenticating => "Authenticating...",
                ConnectionState.WaitingForOtp => "Enter your one-time password",
                ConnectionState.WaitingForSaml => "Complete SAML login in browser",
                ConnectionState.NegotiatingTunnel => "Establishing tunnel...",
                ConnectionState.Connected => $"Connected to {SelectedProfile?.Name ?? "VPN"}",
                ConnectionState.Reconnecting => $"Reconnecting (attempt {conn.ReconnectAttempts})...",
                ConnectionState.Disconnecting => "Disconnecting...",
                ConnectionState.Error => conn.ErrorMessage ?? "Connection error",
                _ => "Unknown state"
            };

            if (newState == ConnectionState.Connected)
            {
                AssignedIp = conn.AssignedIp;
                GatewayIp = conn.GatewayIp;
                DnsInfo = string.Join(", ",
                    new[] { conn.Dns1, conn.Dns2 }.Where(d => d is not null));
                TunnelInterface = conn.TunnelInterface;
                _durationTimer.Start();

                if (SelectedProfile is not null)
                {
                    SelectedProfile.LastConnectedAt = DateTime.UtcNow;
                    _notifications.ShowConnectionEstablished(
                        SelectedProfile.Name, conn.AssignedIp ?? "");
                }
            }
            else if (newState is ConnectionState.Disconnected or ConnectionState.Error)
            {
                _durationTimer.Stop();
                ConnectionDuration = "00:00:00";

                if (HasError)
                {
                    ErrorMessage = conn.ErrorMessage;
                    ErrorSuggestion = conn.ErrorCategory switch
                    {
                        Models.ErrorCategory.AuthenticationFailed =>
                            "Check your username and password, or contact your IT administrator.",
                        Models.ErrorCategory.CertificateError =>
                            "The server certificate is not trusted. Add it to trusted certificates in your profile.",
                        Models.ErrorCategory.NetworkUnreachable =>
                            "Check your internet connection and ensure the VPN server is reachable.",
                        Models.ErrorCategory.PermissionDenied =>
                            "Run the application as Administrator to manage network interfaces.",
                        _ => "Check the log viewer for detailed error information."
                    };
                }

                if (newState == ConnectionState.Disconnected && SelectedProfile is not null)
                {
                    _notifications.ShowConnectionLost(SelectedProfile.Name, null);
                }
            }
        });
    }

    private void OnLogReceived(object? sender, LogEntry entry)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            RecentLogs.Add(entry);
            while (RecentLogs.Count > 50)
                RecentLogs.RemoveAt(0);
        });
    }

    private void UpdateDuration()
    {
        var conn = _vpnService.CurrentConnection;
        if (conn.ConnectedSince.HasValue)
        {
            var elapsed = DateTime.Now - conn.ConnectedSince.Value;
            ConnectionDuration = elapsed.ToString(@"hh\:mm\:ss");
        }
    }
}
