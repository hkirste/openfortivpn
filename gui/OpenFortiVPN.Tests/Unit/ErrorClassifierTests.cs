using FluentAssertions;
using OpenFortiVPN.GUI.Models;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class ErrorClassifierTests
{
    [Fact]
    public void Classify_DnsResolutionFailed()
    {
        var (category, message) = ErrorClassifier.Classify("Could not resolve host vpn.example.com");

        category.Should().Be(ErrorCategory.DnsResolutionFailed);
        message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Classify_NetworkUnreachable()
    {
        var (category, message) = ErrorClassifier.Classify("Could not connect to gateway at 10.0.0.1:443");

        category.Should().Be(ErrorCategory.NetworkUnreachable);
        message.Should().Contain("internet connection");
    }

    [Fact]
    public void Classify_AuthenticationFailed()
    {
        var (category, message) = ErrorClassifier.Classify("VPN authentication failed for user admin");

        category.Should().Be(ErrorCategory.AuthenticationFailed);
        message.Should().Contain("username").And.Contain("password");
    }

    [Fact]
    public void Classify_CertificateVerifyFailed()
    {
        var (category, message) = ErrorClassifier.Classify("SSL certificate verify failed: unable to get local issuer certificate");

        category.Should().Be(ErrorCategory.CertificateError);
        message.Should().Contain("certificate");
    }

    [Fact]
    public void Classify_OperationNotPermitted()
    {
        var (category, message) = ErrorClassifier.Classify("Operation not permitted: could not create tunnel interface");

        category.Should().Be(ErrorCategory.PermissionDenied);
        message.Should().Contain("Administrator");
    }

    [Fact]
    public void Classify_ProcessNotFound()
    {
        var (category, message) = ErrorClassifier.Classify("No such file or directory: openfortivpn");

        category.Should().Be(ErrorCategory.ProcessNotFound);
        message.Should().Contain("not found");
    }

    [Fact]
    public void Classify_UnknownError()
    {
        var (category, message) = ErrorClassifier.Classify("Something completely unexpected happened");

        category.Should().Be(ErrorCategory.Unknown);
        message.Should().Contain("unexpected");
    }

    [Fact]
    public void Classify_CaseInsensitive()
    {
        var (category, _) = ErrorClassifier.Classify("AUTHENTICATION FAILED for user admin");

        category.Should().Be(ErrorCategory.AuthenticationFailed);
    }
}
