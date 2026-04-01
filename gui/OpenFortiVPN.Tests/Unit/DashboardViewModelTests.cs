using FluentAssertions;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;
using OpenFortiVPN.GUI.ViewModels;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class DashboardViewModelTests
{
    private readonly MockVpnService _vpnService = new();
    private readonly MockProfileService _profileService = new();
    private readonly MockCredentialService _credentialService = new();
    private readonly MockNavigationService _navigationService = new();
    private readonly MockNotificationService _notificationService = new();
    private readonly SynchronousDispatcherService _dispatcher = new();

    private DashboardViewModel CreateVm() => new(
        _vpnService, _profileService, _credentialService,
        _navigationService, _notificationService, _dispatcher);

    [Fact]
    public void InitialState_IsDisconnected()
    {
        var vm = CreateVm();
        vm.ConnectionState.Should().Be(ConnectionState.Disconnected);
        vm.StatusText.Should().Be("Ready to connect");
        vm.IsConnected.Should().BeFalse();
        vm.IsConnecting.Should().BeFalse();
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public void OnStateChanged_Connecting_SetsIsConnecting()
    {
        var vm = CreateVm();
        _vpnService.FireStateChanged(ConnectionState.Connecting);

        vm.IsConnecting.Should().BeTrue();
        vm.IsConnected.Should().BeFalse();
        vm.StatusText.Should().Contain("Connecting");
    }

    [Fact]
    public void OnStateChanged_Connected_SetsIsConnected()
    {
        var vm = CreateVm();
        _vpnService.CurrentConnection.AssignedIp = "10.0.0.1";
        _vpnService.CurrentConnection.GatewayIp = "10.0.0.254";
        _vpnService.CurrentConnection.Dns1 = "8.8.8.8";

        _vpnService.FireStateChanged(ConnectionState.Connected);

        vm.IsConnected.Should().BeTrue();
        vm.IsConnecting.Should().BeFalse();
        vm.AssignedIp.Should().Be("10.0.0.1");
        vm.GatewayIp.Should().Be("10.0.0.254");
    }

    [Fact]
    public void OnStateChanged_Error_SetsHasError()
    {
        var vm = CreateVm();
        _vpnService.CurrentConnection.ErrorMessage = "Auth failed";
        _vpnService.CurrentConnection.ErrorCategory = ErrorCategory.AuthenticationFailed;

        _vpnService.FireStateChanged(ConnectionState.Error);

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be("Auth failed");
        vm.ErrorSuggestion.Should().Contain("username");
    }

    [Fact]
    public void OnStateChanged_Error_CertError_GivesCertSuggestion()
    {
        var vm = CreateVm();
        _vpnService.CurrentConnection.ErrorMessage = "Cert failed";
        _vpnService.CurrentConnection.ErrorCategory = ErrorCategory.CertificateError;

        _vpnService.FireStateChanged(ConnectionState.Error);

        vm.ErrorSuggestion.Should().Contain("certificate");
    }

    [Fact]
    public void DismissError_ClearsErrorState()
    {
        var vm = CreateVm();
        vm.HasError = true;
        vm.ErrorMessage = "something";

        vm.DismissErrorCommand.Execute(null);

        vm.HasError.Should().BeFalse();
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void LoadProfiles_PopulatesCollection()
    {
        _profileService.AddProfile(new VpnProfile { Name = "Test VPN" });
        var vm = CreateVm();

        vm.Profiles.Should().HaveCount(1);
        vm.HasProfiles.Should().BeTrue();
        vm.SelectedProfile.Should().NotBeNull();
    }

    [Fact]
    public void LoadProfiles_Empty_HasProfilesFalse()
    {
        var vm = CreateVm();
        vm.HasProfiles.Should().BeFalse();
    }

    [Fact]
    public void OnLogReceived_AddsToRecentLogs()
    {
        var vm = CreateVm();
        var entry = new LogEntry
        {
            Message = "test log",
            Severity = LogSeverity.Info
        };

        _vpnService.FireLogReceived(entry);

        vm.RecentLogs.Should().ContainSingle();
        vm.RecentLogs[0].Message.Should().Be("test log");
    }

    [Fact]
    public void OnLogReceived_CapsAt50()
    {
        var vm = CreateVm();
        for (int i = 0; i < 55; i++)
        {
            _vpnService.FireLogReceived(new LogEntry
            {
                Message = $"log {i}",
                Severity = LogSeverity.Info
            });
        }

        vm.RecentLogs.Should().HaveCount(50);
        vm.RecentLogs[0].Message.Should().Be("log 5");
    }

    // --- Mock implementations ---

    #pragma warning disable CS0067
    private sealed class MockVpnService : IVpnService
    {
        public ConnectionState State { get; set; } = ConnectionState.Disconnected;
        public ConnectionInfo CurrentConnection { get; } = new();
        public bool IsActive => false;

        public event EventHandler<ConnectionState>? StateChanged;
        public event EventHandler<LogEntry>? LogReceived;
        public event EventHandler? OtpRequired;
        public event EventHandler<string>? SamlLoginRequired;

        public Task ConnectAsync(VpnProfile profile, string? password,
            CancellationToken ct = default) => Task.CompletedTask;
        public Task SubmitOtpAsync(string otp) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public void FireStateChanged(ConnectionState s)
        {
            State = s;
            CurrentConnection.State = s;
            StateChanged?.Invoke(this, s);
        }

        public void FireLogReceived(LogEntry e) =>
            LogReceived?.Invoke(this, e);
    }

    #pragma warning disable CS0067
    private sealed class MockProfileService : IProfileService
    {
        private readonly List<VpnProfile> _profiles = new();
        public IReadOnlyList<VpnProfile> Profiles => _profiles;

        public void AddProfile(VpnProfile p) => _profiles.Add(p);
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;
        public Task SaveProfileAsync(VpnProfile p) => Task.CompletedTask;
        public Task DeleteProfileAsync(Guid id) => Task.CompletedTask;
        public VpnProfile ImportFromConfigFile(string path) => new();
        public void ExportToConfigFile(VpnProfile p, string path) { }
        public VpnProfile DuplicateProfile(VpnProfile s) => new();
    }

    #pragma warning disable CS0067
    private sealed class MockCredentialService : ICredentialService
    {
        public void SavePassword(Guid id, string pass) { }
        public string? LoadPassword(Guid id) => null;
        public void DeletePassword(Guid id) { }
        public bool HasPassword(Guid id) => false;
    }

    #pragma warning disable CS0067
    private sealed class MockNavigationService : INavigationService
    {
        public CommunityToolkit.Mvvm.ComponentModel.ObservableObject CurrentViewModel { get; set; } = null!;
        public event EventHandler<CommunityToolkit.Mvvm.ComponentModel.ObservableObject>? Navigated;
        public bool CanGoBack => false;
        public void NavigateTo<T>() where T : CommunityToolkit.Mvvm.ComponentModel.ObservableObject { }
        public void NavigateTo<T>(object p) where T : CommunityToolkit.Mvvm.ComponentModel.ObservableObject { }
        public void GoBack() { }
    }

    #pragma warning disable CS0067
    private sealed class MockNotificationService : INotificationService
    {
        public void ShowInfo(string t, string m) { }
        public void ShowWarning(string t, string m) { }
        public void ShowError(string t, string m) { }
        public void ShowConnectionEstablished(string n, string ip) { }
        public void ShowConnectionLost(string n, string? r) { }
        public void ShowReconnecting(string n, int a) { }
    }
}
#pragma warning restore CS0067
