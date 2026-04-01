using System.Globalization;
using System.Windows;
using FluentAssertions;
using OpenFortiVPN.GUI.Converters;
using OpenFortiVPN.GUI.Models;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class ConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // --- BoolToVisibilityConverter ---

    [Fact]
    public void BoolToVisibility_True_ReturnsVisible()
    {
        var c = new BoolToVisibilityConverter();
        c.Convert(true, typeof(Visibility), null!, Culture)
            .Should().Be(Visibility.Visible);
    }

    [Fact]
    public void BoolToVisibility_False_ReturnsCollapsed()
    {
        var c = new BoolToVisibilityConverter();
        c.Convert(false, typeof(Visibility), null!, Culture)
            .Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void BoolToVisibility_TrueInverted_ReturnsCollapsed()
    {
        var c = new BoolToVisibilityConverter();
        c.Convert(true, typeof(Visibility), "Invert", Culture)
            .Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void BoolToVisibility_FalseInverted_ReturnsVisible()
    {
        var c = new BoolToVisibilityConverter();
        c.Convert(false, typeof(Visibility), "Invert", Culture)
            .Should().Be(Visibility.Visible);
    }

    [Fact]
    public void BoolToVisibility_ConvertBack_Visible_ReturnsTrue()
    {
        var c = new BoolToVisibilityConverter();
        c.ConvertBack(Visibility.Visible, typeof(bool), null!, Culture)
            .Should().Be(true);
    }

    [Fact]
    public void BoolToVisibility_ConvertBack_Collapsed_ReturnsFalse()
    {
        var c = new BoolToVisibilityConverter();
        c.ConvertBack(Visibility.Collapsed, typeof(bool), null!, Culture)
            .Should().Be(false);
    }

    // --- ConnectionStateToBrushConverter ---

    [Theory]
    [InlineData(ConnectionState.Connected)]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Authenticating)]
    [InlineData(ConnectionState.Disconnected)]
    [InlineData(ConnectionState.Error)]
    [InlineData(ConnectionState.Reconnecting)]
    [InlineData(ConnectionState.WaitingForOtp)]
    [InlineData(ConnectionState.WaitingForSaml)]
    [InlineData(ConnectionState.Disconnecting)]
    [InlineData(ConnectionState.NegotiatingTunnel)]
    public void ConnectionStateToBrush_AllStates_ReturnsBrush(
        ConnectionState state)
    {
        var c = new ConnectionStateToBrushConverter();
        var result = c.Convert(state, typeof(object), null!, Culture);
        result.Should().NotBeNull();
    }

    [Fact]
    public void ConnectionStateToBrush_InvalidInput_ReturnsBrush()
    {
        var c = new ConnectionStateToBrushConverter();
        var result = c.Convert("not a state", typeof(object), null!, Culture);
        result.Should().NotBeNull();
    }

    // --- ConnectionStateToIconConverter ---

    [Theory]
    [InlineData(ConnectionState.Connected)]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Error)]
    [InlineData(ConnectionState.Disconnected)]
    public void ConnectionStateToIcon_AllStates_ReturnsString(
        ConnectionState state)
    {
        var c = new ConnectionStateToIconConverter();
        var result = c.Convert(state, typeof(string), null!, Culture);
        result.Should().BeOfType<string>();
        ((string)result).Should().NotBeEmpty();
    }

    // --- LogSeverityToBrushConverter ---

    [Theory]
    [InlineData(LogSeverity.Error)]
    [InlineData(LogSeverity.Warning)]
    [InlineData(LogSeverity.Info)]
    [InlineData(LogSeverity.Debug)]
    public void LogSeverityToBrush_AllSeverities_ReturnsBrush(
        LogSeverity severity)
    {
        var c = new LogSeverityToBrushConverter();
        var result = c.Convert(severity, typeof(object), null!, Culture);
        result.Should().NotBeNull();
    }

    // --- EnumToDisplayConverter ---

    [Theory]
    [InlineData(TlsVersion.Default, "System Default")]
    [InlineData(TlsVersion.Tls12, "TLS 1.2")]
    [InlineData(TlsVersion.Tls13, "TLS 1.3")]
    public void EnumToDisplay_TlsVersion_ReturnsDisplayString(
        TlsVersion version, string expected)
    {
        var c = new EnumToDisplayConverter();
        c.Convert(version, typeof(string), null!, Culture)
            .Should().Be(expected);
    }
}
