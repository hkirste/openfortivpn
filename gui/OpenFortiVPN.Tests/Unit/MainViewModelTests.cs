using CommunityToolkit.Mvvm.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;
using OpenFortiVPN.GUI.ViewModels;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class MainViewModelTests
{
    private readonly MockVpnService _vpnService = new();
    private readonly MockSettingsService _settings = new();
    private readonly SynchronousDispatcherService _dispatcher = new();

    private MainViewModel CreateVm()
    {
        // NavigationService needs a real DI container to resolve VMs
        var services = new ServiceCollection();
        services.AddSingleton<IVpnService>(_vpnService);
        services.AddSingleton<ISettingsService>(_settings);
        services.AddSingleton<IDispatcherService>(_dispatcher);
        services.AddSingleton<IProfileService>(new MockProfileService());
        services.AddSingleton<ICredentialService>(new MockCredentialService());
        services.AddSingleton<INotificationService>(new MockNotificationService());
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProfileListViewModel>();
        services.AddTransient<LogViewerViewModel>();
        services.AddTransient<SettingsViewModel>();
        var sp = services.BuildServiceProvider();

        return new MainViewModel(
            sp.GetRequiredService<INavigationService>(),
            _vpnService, _settings, _dispatcher);
    }

    [Fact]
    public void InitialState_StartsOnDashboard()
    {
        var vm = CreateVm();
        vm.SelectedNavItem.Should().Be("Dashboard");
        vm.CurrentView.Should().BeOfType<DashboardViewModel>();
    }

    [Fact]
    public void InitialState_IsDisconnected()
    {
        var vm = CreateVm();
        vm.ConnectionState.Should().Be(ConnectionState.Disconnected);
        vm.StatusText.Should().Be("Disconnected");
    }

    [Fact]
    public void OnStateChanged_Connecting_UpdatesStatus()
    {
        var vm = CreateVm();
        _vpnService.FireStateChanged(ConnectionState.Connecting);

        vm.ConnectionState.Should().Be(ConnectionState.Connecting);
        vm.StatusText.Should().Be("Connecting...");
        vm.TrayTooltip.Should().Contain("Connecting");
    }

    [Fact]
    public void OnStateChanged_Connected_UpdatesStatus()
    {
        var vm = CreateVm();
        _vpnService.FireStateChanged(ConnectionState.Connected);

        vm.ConnectionState.Should().Be(ConnectionState.Connected);
        vm.StatusText.Should().Be("Connected");
    }

    [Fact]
    public void OnStateChanged_Error_UpdatesStatus()
    {
        var vm = CreateVm();
        _vpnService.FireStateChanged(ConnectionState.Error);

        vm.StatusText.Should().Be("Error");
    }

    [Fact]
    public void OnStateChanged_AllStates_ProduceStatusText()
    {
        var vm = CreateVm();
        foreach (ConnectionState state in Enum.GetValues<ConnectionState>())
        {
            _vpnService.FireStateChanged(state);
            vm.StatusText.Should().NotBeNullOrEmpty(
                $"state {state} should produce status text");
        }
    }

    [Fact]
    public void NavigateTo_Profiles_ChangesView()
    {
        var vm = CreateVm();
        vm.NavigateToCommand.Execute("Profiles");

        vm.SelectedNavItem.Should().Be("Profiles");
        vm.CurrentView.Should().BeOfType<ProfileListViewModel>();
    }

    [Fact]
    public void NavigateTo_Logs_ChangesView()
    {
        var vm = CreateVm();
        vm.NavigateToCommand.Execute("Logs");

        vm.CurrentView.Should().BeOfType<LogViewerViewModel>();
    }

    [Fact]
    public void NavigateTo_Settings_ChangesView()
    {
        var vm = CreateVm();
        vm.NavigateToCommand.Execute("Settings");

        vm.CurrentView.Should().BeOfType<SettingsViewModel>();
    }

    [Fact]
    public void ShowWindow_SetsVisible()
    {
        var vm = CreateVm();
        vm.IsWindowVisible = false;

        vm.ShowWindowCommand.Execute(null);

        vm.IsWindowVisible.Should().BeTrue();
    }

    [Fact]
    public void HideWindow_WhenMinimizeToTray_Hides()
    {
        _settings.Current.MinimizeToTray = true;
        var vm = CreateVm();

        vm.HideWindowCommand.Execute(null);

        vm.IsWindowVisible.Should().BeFalse();
    }

    [Fact]
    public void HideWindow_WhenNotMinimizeToTray_StaysVisible()
    {
        _settings.Current.MinimizeToTray = false;
        var vm = CreateVm();

        vm.HideWindowCommand.Execute(null);

        vm.IsWindowVisible.Should().BeTrue();
    }

    [Fact]
    public async Task QuickDisconnect_WhenNotActive_DoesNothing()
    {
        var vm = CreateVm();
        _vpnService.IsActiveValue = false;

        await vm.QuickDisconnectCommand.ExecuteAsync(null);

        _vpnService.DisconnectCalled.Should().BeFalse();
    }

    [Fact]
    public async Task QuickDisconnect_WhenActive_Disconnects()
    {
        var vm = CreateVm();
        _vpnService.IsActiveValue = true;

        await vm.QuickDisconnectCommand.ExecuteAsync(null);

        _vpnService.DisconnectCalled.Should().BeTrue();
    }

    // --- Mocks ---

    #pragma warning disable CS0067
    private sealed class MockVpnService : IVpnService
    {
        public ConnectionState State { get; set; } = ConnectionState.Disconnected;
        public ConnectionInfo CurrentConnection { get; } = new();
        public bool IsActiveValue { get; set; }
        public bool IsActive => IsActiveValue;
        public IReadOnlyList<LogEntry> LogBuffer => new List<LogEntry>();
        public bool DisconnectCalled { get; private set; }

        public event EventHandler<ConnectionState>? StateChanged;
        public event EventHandler<LogEntry>? LogReceived;
        public event EventHandler? OtpRequired;
        public event EventHandler<string>? SamlLoginRequired;

        public Task ConnectAsync(VpnProfile p, string? pw,
            CancellationToken ct = default) => Task.CompletedTask;
        public Task SubmitOtpAsync(string otp) => Task.CompletedTask;
        public Task DisconnectAsync()
        {
            DisconnectCalled = true;
            return Task.CompletedTask;
        }

        public void FireStateChanged(ConnectionState s)
        {
            State = s;
            StateChanged?.Invoke(this, s);
        }
    }

    #pragma warning disable CS0067
    private sealed class MockSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;
    }

    #pragma warning disable CS0067
    private sealed class MockProfileService : IProfileService
    {
        public IReadOnlyList<VpnProfile> Profiles => new List<VpnProfile>();
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
        public void SavePassword(Guid id, string pw) { }
        public string? LoadPassword(Guid id) => null;
        public void DeletePassword(Guid id) { }
        public bool HasPassword(Guid id) => false;
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
