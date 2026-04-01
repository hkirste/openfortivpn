using FluentAssertions;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;
using OpenFortiVPN.GUI.ViewModels;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class LogViewerViewModelTests
{
    private readonly MockVpnService _vpnService = new();
    private readonly MockSettingsService _settings = new();
    private readonly SynchronousDispatcherService _dispatcher = new();

    private LogViewerViewModel CreateVm() => new(
        _vpnService, _settings, _dispatcher);

    [Fact]
    public void InitialState_IsEmpty()
    {
        var vm = CreateVm();
        vm.FilteredLogs.Should().BeEmpty();
        vm.TotalEntries.Should().Be(0);
        vm.VisibleEntries.Should().Be(0);
    }

    [Fact]
    public void OnLogReceived_AddsToFilteredLogs()
    {
        var vm = CreateVm();
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "test",
            Severity = LogSeverity.Info
        });

        vm.FilteredLogs.Should().HaveCount(1);
        vm.TotalEntries.Should().Be(1);
        vm.VisibleEntries.Should().Be(1);
    }

    [Fact]
    public void ShowDebug_False_FiltersDebugEntries()
    {
        var vm = CreateVm();
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "debug msg",
            Severity = LogSeverity.Debug
        });
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "info msg",
            Severity = LogSeverity.Info
        });

        vm.FilteredLogs.Should().HaveCount(2);

        vm.ShowDebug = false;

        vm.FilteredLogs.Should().HaveCount(1);
        vm.FilteredLogs[0].Message.Should().Be("info msg");
    }

    [Fact]
    public void ShowErrors_False_FiltersErrorEntries()
    {
        var vm = CreateVm();
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "error msg",
            Severity = LogSeverity.Error
        });
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "info msg",
            Severity = LogSeverity.Info
        });

        vm.ShowErrors = false;

        vm.FilteredLogs.Should().HaveCount(1);
        vm.FilteredLogs[0].Severity.Should().Be(LogSeverity.Info);
    }

    [Fact]
    public void SearchText_FiltersMessages()
    {
        var vm = CreateVm();
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "connecting to gateway",
            Severity = LogSeverity.Info
        });
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "authentication started",
            Severity = LogSeverity.Info
        });

        vm.SearchText = "gateway";

        vm.FilteredLogs.Should().HaveCount(1);
        vm.FilteredLogs[0].Message.Should().Contain("gateway");
    }

    [Fact]
    public void SearchText_CaseInsensitive()
    {
        var vm = CreateVm();
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "Connected to Gateway",
            Severity = LogSeverity.Info
        });

        vm.SearchText = "gateway";

        vm.FilteredLogs.Should().HaveCount(1);
    }

    [Fact]
    public void ClearLogs_RemovesAll()
    {
        var vm = CreateVm();
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "test",
            Severity = LogSeverity.Info
        });
        vm.FilteredLogs.Should().HaveCount(1);

        vm.ClearLogsCommand.Execute(null);

        vm.FilteredLogs.Should().BeEmpty();
        vm.TotalEntries.Should().Be(0);
        vm.VisibleEntries.Should().Be(0);
    }

    [Fact]
    public void MaxLogLines_EnforcesLimit()
    {
        _settings.Current.MaxLogLines = 5;
        var vm = CreateVm();

        for (int i = 0; i < 10; i++)
        {
            _vpnService.FireLogReceived(new LogEntry
            {
                Message = $"msg {i}",
                Severity = LogSeverity.Info
            });
        }

        vm.TotalEntries.Should().Be(5);
    }

    [Fact]
    public void CombinedFilters_SeverityAndSearch()
    {
        var vm = CreateVm();
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "DEBUG gateway check",
            Severity = LogSeverity.Debug
        });
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "INFO gateway connected",
            Severity = LogSeverity.Info
        });
        _vpnService.FireLogReceived(new LogEntry
        {
            Message = "INFO other stuff",
            Severity = LogSeverity.Info
        });

        vm.ShowDebug = false;
        vm.SearchText = "gateway";

        vm.FilteredLogs.Should().HaveCount(1);
        vm.FilteredLogs[0].Message.Should().Be("INFO gateway connected");
    }

    // --- Mocks ---

    #pragma warning disable CS0067
    private sealed class MockVpnService : IVpnService
    {
        public ConnectionState State { get; set; }
        public ConnectionInfo CurrentConnection { get; } = new();
        public bool IsActive => false;

        public event EventHandler<ConnectionState>? StateChanged;
        public event EventHandler<LogEntry>? LogReceived;
        public event EventHandler? OtpRequired;
        public event EventHandler<string>? SamlLoginRequired;

        public Task ConnectAsync(VpnProfile p, string? pw,
            CancellationToken ct = default) => Task.CompletedTask;
        public Task SubmitOtpAsync(string otp) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;

        public void FireLogReceived(LogEntry e) =>
            LogReceived?.Invoke(this, e);
    }

    #pragma warning disable CS0067
    private sealed class MockSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;
    }
}
#pragma warning restore CS0067
