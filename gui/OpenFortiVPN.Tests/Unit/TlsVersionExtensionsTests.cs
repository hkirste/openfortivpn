using FluentAssertions;
using OpenFortiVPN.GUI.Models;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class TlsVersionExtensionsTests
{
    [Theory]
    [InlineData(TlsVersion.Tls10, "1.0")]
    [InlineData(TlsVersion.Tls11, "1.1")]
    [InlineData(TlsVersion.Tls12, "1.2")]
    [InlineData(TlsVersion.Tls13, "1.3")]
    [InlineData(TlsVersion.Default, "")]
    public void ToCliValue_AllVersions(TlsVersion version, string expected)
    {
        version.ToCliValue().Should().Be(expected);
    }

    [Theory]
    [InlineData(TlsVersion.Tls10, "TLS 1.0")]
    [InlineData(TlsVersion.Tls11, "TLS 1.1")]
    [InlineData(TlsVersion.Tls12, "TLS 1.2")]
    [InlineData(TlsVersion.Tls13, "TLS 1.3")]
    [InlineData(TlsVersion.Default, "System Default")]
    public void ToDisplayString_AllVersions(TlsVersion version, string expected)
    {
        version.ToDisplayString().Should().Be(expected);
    }
}
