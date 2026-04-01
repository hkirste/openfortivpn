using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class ProfileServiceExportTests : IDisposable
{
    private readonly ProfileService _service;
    private readonly List<string> _tempFiles = new();

    public ProfileServiceExportTests()
    {
        _service = new ProfileService(
            new NullLogger<ProfileService>());
    }

    private string CreateTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(),
            $"ofv-test-{Guid.NewGuid():N}.conf");
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void ExportToConfigFile_BasicProfile_WritesExpectedFormat()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            GatewayPort = 8443,
            Username = "john.doe"
        };
        var path = CreateTempFile();

        _service.ExportToConfigFile(profile, path);

        var content = File.ReadAllText(path);
        content.Should().Contain("host = vpn.example.com");
        content.Should().Contain("port = 8443");
        content.Should().Contain("username = john.doe");
    }

    [Fact]
    public void ExportToConfigFile_TrustedCerts_WritesAll()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            TrustedCertDigests = new List<string> { "aabb", "ccdd" }
        };
        var path = CreateTempFile();

        _service.ExportToConfigFile(profile, path);

        var content = File.ReadAllText(path);
        content.Should().Contain("trusted-cert = aabb");
        content.Should().Contain("trusted-cert = ccdd");
    }

    [Fact]
    public void ExportToConfigFile_ConditionalFields_OnlyWrittenWhenSet()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            Realm = null,
            CipherList = null,
            SniOverride = null
        };
        var path = CreateTempFile();

        _service.ExportToConfigFile(profile, path);

        var content = File.ReadAllText(path);
        content.Should().NotContain("realm");
        content.Should().NotContain("cipher-list");
        content.Should().NotContain("sni");
    }

    [Fact]
    public void ImportExport_RoundTrip_PreservesAllValues()
    {
        var original = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            GatewayPort = 10443,
            Username = "john.doe",
            Realm = "CORP",
            SetRoutes = false,
            SetDns = false,
            HalfInternetRoutes = true,
            InsecureSsl = true,
            MinTlsVersion = TlsVersion.Tls12,
            SecurityLevel1 = true,
            PersistentInterval = 30,
            TrustedCertDigests = new List<string> { "aabb", "ccdd" }
        };
        var path = CreateTempFile();

        _service.ExportToConfigFile(original, path);
        var imported = _service.ImportFromConfigFile(path);

        imported.GatewayHost.Should().Be(original.GatewayHost);
        imported.GatewayPort.Should().Be(original.GatewayPort);
        imported.Username.Should().Be(original.Username);
        imported.Realm.Should().Be(original.Realm);
        imported.SetRoutes.Should().Be(original.SetRoutes);
        imported.SetDns.Should().Be(original.SetDns);
        imported.HalfInternetRoutes.Should().Be(original.HalfInternetRoutes);
        imported.InsecureSsl.Should().Be(original.InsecureSsl);
        imported.MinTlsVersion.Should().Be(original.MinTlsVersion);
        imported.SecurityLevel1.Should().Be(original.SecurityLevel1);
        imported.PersistentInterval.Should().Be(original.PersistentInterval);
        imported.TrustedCertDigests.Should().BeEquivalentTo(
            original.TrustedCertDigests);
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }
}
