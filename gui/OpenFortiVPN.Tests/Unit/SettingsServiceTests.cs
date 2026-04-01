using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class SettingsServiceTests
{
    [Fact]
    public void Current_Defaults_AreCorrect()
    {
        var s = new AppSettings();

        s.StartWithWindows.Should().BeFalse();
        s.StartMinimized.Should().BeFalse();
        s.AutoConnectLastProfile.Should().BeFalse();
        s.MinimizeToTray.Should().BeFalse();
        s.CloseToTray.Should().BeFalse();
        s.ShowNotifications.Should().BeTrue();
        s.ShowConnectionDuration.Should().BeTrue();
        s.LogVerbosity.Should().Be(VpnLogLevel.Info);
        s.MaxLogRetentionDays.Should().Be(30);
        s.MaxLogLines.Should().Be(10_000);
        s.OpenFortiVpnPath.Should().Be("openfortivpn.exe");
        s.Theme.Should().Be(ThemeMode.System);
    }

    [Fact]
    public void Current_SettingsService_ReturnsNonNull()
    {
        var service = new SettingsService(new NullLogger<SettingsService>());
        service.Current.Should().NotBeNull();
    }

    [Fact]
    public void Current_Mutating_PersistsOnSameInstance()
    {
        var service = new SettingsService(new NullLogger<SettingsService>());
        service.Current.StartWithWindows = true;
        service.Current.Theme = ThemeMode.Dark;

        service.Current.StartWithWindows.Should().BeTrue();
        service.Current.Theme.Should().Be(ThemeMode.Dark);
    }
}
