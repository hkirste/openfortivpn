using FluentAssertions;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class ExitCodeMappingTests
{
    [Fact]
    public void MapExitCode_0_ReturnsNull()
    {
        VpnService.MapExitCode(0).Should().BeNull();
    }

    [Fact]
    public void MapExitCode_10_DnsResolutionFailed()
    {
        var result = VpnService.MapExitCode(10);
        result.Should().NotBeNull();
        result!.Value.Category.Should().Be(ErrorCategory.DnsResolutionFailed);
    }

    [Fact]
    public void MapExitCode_11_NetworkUnreachable()
    {
        var result = VpnService.MapExitCode(11);
        result!.Value.Category.Should().Be(ErrorCategory.NetworkUnreachable);
    }

    [Fact]
    public void MapExitCode_12_CertificateError()
    {
        var result = VpnService.MapExitCode(12);
        result!.Value.Category.Should().Be(ErrorCategory.CertificateError);
    }

    [Fact]
    public void MapExitCode_13_CertificateError()
    {
        var result = VpnService.MapExitCode(13);
        result!.Value.Category.Should().Be(ErrorCategory.CertificateError);
    }

    [Fact]
    public void MapExitCode_20_AuthenticationFailed()
    {
        var result = VpnService.MapExitCode(20);
        result!.Value.Category.Should().Be(ErrorCategory.AuthenticationFailed);
    }

    [Fact]
    public void MapExitCode_50_PermissionDenied()
    {
        var result = VpnService.MapExitCode(50);
        result!.Value.Category.Should().Be(ErrorCategory.PermissionDenied);
    }

    [Fact]
    public void MapExitCode_Unknown_ProcessCrashed()
    {
        var result = VpnService.MapExitCode(99);
        result!.Value.Category.Should().Be(ErrorCategory.ProcessCrashed);
        result!.Value.Message.Should().Contain("99");
    }

    [Theory]
    [InlineData("network_unreachable", ErrorCategory.NetworkUnreachable)]
    [InlineData("dns_resolution_failed", ErrorCategory.DnsResolutionFailed)]
    [InlineData("authentication_failed", ErrorCategory.AuthenticationFailed)]
    [InlineData("certificate_error", ErrorCategory.CertificateError)]
    [InlineData("tunnel_setup_failed", ErrorCategory.TunnelSetupFailed)]
    [InlineData("permission_denied", ErrorCategory.PermissionDenied)]
    [InlineData("configuration_error", ErrorCategory.ConfigurationError)]
    [InlineData("timeout", ErrorCategory.Timeout)]
    [InlineData("something_unknown", ErrorCategory.Unknown)]
    public void MapErrorCategory_AllCategories(string input, ErrorCategory expected)
    {
        VpnService.MapErrorCategory(input).Should().Be(expected);
    }
}
