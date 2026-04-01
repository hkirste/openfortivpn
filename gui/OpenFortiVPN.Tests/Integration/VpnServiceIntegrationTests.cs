using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;
using Xunit;

namespace OpenFortiVPN.Tests.Integration;

/// <summary>
/// Integration tests that spawn the test harness as a real child process
/// and verify the VpnService state machine end-to-end.
/// </summary>
[Collection("Integration")]
public class VpnServiceIntegrationTests : IDisposable
{
    private readonly string _harnessPath;
    private VpnService? _svc;

    public VpnServiceIntegrationTests()
    {
        // Locate the harness exe relative to the test assembly
        var testDir = Path.GetDirectoryName(
            typeof(VpnServiceIntegrationTests).Assembly.Location)!;
        _harnessPath = Path.Combine(testDir, "openfortivpn-harness.exe");
        if (!File.Exists(_harnessPath))
        {
            // Try .dll for non-Windows
            var dll = Path.ChangeExtension(_harnessPath, ".dll");
            if (File.Exists(dll))
                _harnessPath = dll;
        }
    }

    private VpnService CreateService(string scenario, int delayMs = 20)
    {
        Environment.SetEnvironmentVariable("SCENARIO", scenario);
        Environment.SetEnvironmentVariable("DELAY_MS", delayMs.ToString());

        var settings = new TestSettingsService(_harnessPath);
        var svc = new VpnService(settings,
            new NullLogger<VpnService>());
        _svc = svc;
        return svc;
    }

    private static VpnProfile TestProfile() => new()
    {
        GatewayHost = "test.example.com",
        Username = "testuser"
    };

    [Fact]
    public async Task CertError_TransitionsToError()
    {
        using var svc = CreateService("cert_error");
        var states = new List<ConnectionState>();
        var done = new TaskCompletionSource();

        svc.StateChanged += (_, s) =>
        {
            states.Add(s);
            if (s == ConnectionState.Error)
                done.TrySetResult();
        };

        await svc.ConnectAsync(TestProfile(), "testpass");
        var completed = await Task.WhenAny(done.Task, Task.Delay(10_000));

        completed.Should().Be(done.Task, "should reach Error state within timeout");
        svc.CurrentConnection.ErrorCategory.Should()
            .Be(ErrorCategory.CertificateError);
    }

    [Fact]
    public async Task AuthFailed_TransitionsToError()
    {
        using var svc = CreateService("auth_failed");
        var states = new List<ConnectionState>();
        var done = new TaskCompletionSource();

        svc.StateChanged += (_, s) =>
        {
            states.Add(s);
            if (s == ConnectionState.Error)
                done.TrySetResult();
        };

        await svc.ConnectAsync(TestProfile(), "wrongpass");
        var completed = await Task.WhenAny(done.Task, Task.Delay(10_000));

        completed.Should().Be(done.Task);
        svc.CurrentConnection.ErrorCategory.Should()
            .Be(ErrorCategory.AuthenticationFailed);
    }

    [Fact]
    public async Task DnsFailed_TransitionsToError()
    {
        using var svc = CreateService("dns_failed");
        var done = new TaskCompletionSource();

        svc.StateChanged += (_, s) =>
        {
            if (s == ConnectionState.Error)
                done.TrySetResult();
        };

        await svc.ConnectAsync(TestProfile(), null);
        var completed = await Task.WhenAny(done.Task, Task.Delay(10_000));

        completed.Should().Be(done.Task);
        svc.CurrentConnection.ErrorCategory.Should()
            .Be(ErrorCategory.DnsResolutionFailed);
    }

    [Fact]
    public async Task PermissionDenied_TransitionsToError()
    {
        using var svc = CreateService("permission_denied");
        var done = new TaskCompletionSource();

        svc.StateChanged += (_, s) =>
        {
            if (s == ConnectionState.Error)
                done.TrySetResult();
        };

        await svc.ConnectAsync(TestProfile(), null);
        var completed = await Task.WhenAny(done.Task, Task.Delay(10_000));

        completed.Should().Be(done.Task);
        svc.CurrentConnection.ErrorCategory.Should()
            .Be(ErrorCategory.PermissionDenied);
    }

    [Fact]
    public async Task SuccessfulConnect_ReachesConnected()
    {
        using var svc = CreateService("successful_connect");
        var done = new TaskCompletionSource();

        svc.StateChanged += (_, s) =>
        {
            if (s == ConnectionState.Connected)
                done.TrySetResult();
        };

        await svc.ConnectAsync(TestProfile(), "testpass");
        var completed = await Task.WhenAny(done.Task, Task.Delay(10_000));

        completed.Should().Be(done.Task, "should reach Connected within timeout");
        svc.CurrentConnection.AssignedIp.Should().Be("10.211.1.42");
        svc.CurrentConnection.Dns1.Should().Be("10.211.1.1");
    }

    [Fact]
    public async Task SuccessfulConnect_ThenDisconnect()
    {
        using var svc = CreateService("successful_connect");
        var connected = new TaskCompletionSource();
        var disconnected = new TaskCompletionSource();

        svc.StateChanged += (_, s) =>
        {
            if (s == ConnectionState.Connected)
                connected.TrySetResult();
            if (s == ConnectionState.Disconnected)
                disconnected.TrySetResult();
        };

        await svc.ConnectAsync(TestProfile(), "testpass");
        await Task.WhenAny(connected.Task, Task.Delay(10_000));
        connected.Task.IsCompleted.Should().BeTrue();

        await svc.DisconnectAsync();
        var completed = await Task.WhenAny(disconnected.Task, Task.Delay(10_000));

        completed.Should().Be(disconnected.Task);
        svc.State.Should().Be(ConnectionState.Disconnected);
    }

    public void Dispose()
    {
        _svc?.Dispose();
        Environment.SetEnvironmentVariable("SCENARIO", null);
        Environment.SetEnvironmentVariable("DELAY_MS", null);
    }

    /// <summary>
    /// Minimal ISettingsService that points to the test harness.
    /// </summary>
    private sealed class TestSettingsService : ISettingsService
    {
        public AppSettings Current { get; }

        public TestSettingsService(string harnessPath)
        {
            Current = new AppSettings { OpenFortiVpnPath = harnessPath };
        }

        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;
    }
}
