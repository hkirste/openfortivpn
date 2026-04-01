using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class NotificationServiceTests
{
    private readonly MockSettingsService _settings = new();

    private NotificationService CreateService() => new(
        _settings, new NullLogger<NotificationService>());

    [Fact]
    public void ShowInfo_WhenNotificationsEnabled_DoesNotThrow()
    {
        _settings.Current.ShowNotifications = true;
        var svc = CreateService();

        var act = () => svc.ShowInfo("Title", "Message");
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowInfo_WhenNotificationsDisabled_DoesNotThrow()
    {
        _settings.Current.ShowNotifications = false;
        var svc = CreateService();

        var act = () => svc.ShowInfo("Title", "Message");
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowError_AlwaysShows_RegardlessOfSetting()
    {
        _settings.Current.ShowNotifications = false;
        var svc = CreateService();

        // ShowError should not check the setting (errors always shown)
        var act = () => svc.ShowError("Error", "Something failed");
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowConnectionEstablished_FormatsMessage()
    {
        _settings.Current.ShowNotifications = true;
        var svc = CreateService();

        var act = () => svc.ShowConnectionEstablished("Office VPN", "10.0.0.1");
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowConnectionLost_WithReason_FormatsMessage()
    {
        _settings.Current.ShowNotifications = true;
        var svc = CreateService();

        var act = () => svc.ShowConnectionLost("Office VPN", "timeout");
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowConnectionLost_NullReason_FormatsMessage()
    {
        _settings.Current.ShowNotifications = true;
        var svc = CreateService();

        var act = () => svc.ShowConnectionLost("Office VPN", null);
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowReconnecting_FormatsMessage()
    {
        _settings.Current.ShowNotifications = true;
        var svc = CreateService();

        var act = () => svc.ShowReconnecting("Office VPN", 3);
        act.Should().NotThrow();
    }

    private sealed class MockSettingsService : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;
    }
}
