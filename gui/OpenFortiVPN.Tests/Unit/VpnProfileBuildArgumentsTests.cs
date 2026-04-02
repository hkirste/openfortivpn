using FluentAssertions;
using OpenFortiVPN.GUI.Models;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class VpnProfileBuildArgumentsTests
{
    [Fact]
    public void BuildArguments_MinimalProfile_HostIsLast()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            GatewayPort = 443,
            Username = "user1"
        };

        var args = profile.BuildArguments();

        args.Should().NotBeEmpty();
        args.Last().Should().Be("vpn.example.com");
    }

    [Fact]
    public void BuildArguments_NonDefaultPort_HostPortFormat()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            GatewayPort = 10443,
            Username = "user1"
        };

        var args = profile.BuildArguments();

        args.Last().Should().Be("vpn.example.com:10443");
    }

    [Fact]
    public void BuildArguments_NeverContainsPassword()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            Username = "user1"
        };

        var args = profile.BuildArguments();

        args.Should().NotContain(a => a.Contains("password", StringComparison.OrdinalIgnoreCase));
        args.Should().NotContain("-p");
        args.Should().NotContain("--password");
    }

    [Fact]
    public void BuildArguments_Realm_IncludesRealmFlag()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            Username = "user1",
            Realm = "CORP"
        };

        var args = profile.BuildArguments();

        args.Should().Contain("--realm=CORP");
    }

    [Fact]
    public void BuildArguments_UserCert_IncludesCertFlag()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            Username = "user1",
            UserCertPath = @"C:\certs\user.pem"
        };

        var args = profile.BuildArguments();

        args.Should().Contain(@"--user-cert=C:\certs\user.pem");
    }

    [Fact]
    public void BuildArguments_NoRoutes_IncludesFlag()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            Username = "user1",
            SetRoutes = false
        };

        var args = profile.BuildArguments();

        args.Should().Contain("--no-routes");
    }

    [Fact]
    public void BuildArguments_InsecureSsl_IncludesFlag()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            Username = "user1",
            InsecureSsl = true
        };

        var args = profile.BuildArguments();

        args.Should().Contain("--insecure-ssl");
    }

    [Fact]
    public void BuildArguments_Persistent_IncludesFlag()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            Username = "user1",
            PersistentInterval = 30
        };

        var args = profile.BuildArguments();

        args.Should().Contain("--persistent=30");
    }

    [Fact]
    public void BuildArguments_PersistentZero_NoFlag()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            Username = "user1",
            PersistentInterval = 0
        };

        var args = profile.BuildArguments();

        args.Should().NotContain(a => a.StartsWith("--persistent"));
    }

    [Fact]
    public void BuildArguments_DoesNotHardcodeVerbosity()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            Username = "user1"
        };

        var args = profile.BuildArguments();

        // Verbosity is controlled by settings, not profile
        args.Should().NotContain("-v");
        args.Should().NotContain("-q");
    }

    [Fact]
    public void BuildArguments_EmptyHost_DoesNotCrash()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "",
            Username = "user1"
        };

        var args = profile.BuildArguments();

        args.Should().NotBeNull();
        // Host should not appear when empty
        args.Should().NotContain(a => a == "" || a.Contains(":443"));
    }

    [Fact]
    public void BuildArguments_HostIsAlwaysLastElement()
    {
        var profile = new VpnProfile
        {
            GatewayHost = "vpn.example.com",
            GatewayPort = 10443,
            Username = "admin",
            Realm = "CORP",
            UserCertPath = @"C:\cert.pem",
            UserKeyPath = @"C:\key.pem",
            InsecureSsl = true,
            SetRoutes = false,
            SetDns = false,
            PersistentInterval = 60,
            HalfInternetRoutes = true
        };

        var args = profile.BuildArguments();

        args.Should().NotBeEmpty();
        args.Last().Should().Be("vpn.example.com:10443");

        // Verify all expected flags are present
        args.Should().Contain("--realm=CORP");
        args.Should().Contain(@"--user-cert=C:\cert.pem");
        args.Should().Contain(@"--user-key=C:\key.pem");
        args.Should().Contain("--insecure-ssl");
        args.Should().Contain("--no-routes");
        args.Should().Contain("--no-dns");
        args.Should().Contain("--persistent=60");
        args.Should().Contain("--half-internet-routes=1");
    }
}
