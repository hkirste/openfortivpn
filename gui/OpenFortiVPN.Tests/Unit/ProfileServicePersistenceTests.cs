using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class ProfileServicePersistenceTests
{
    private ProfileService CreateService()
    {
        return new ProfileService(new NullLogger<ProfileService>());
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesProfiles()
    {
        var service = CreateService();
        var profile = new VpnProfile
        {
            Name = "Test VPN",
            GatewayHost = "vpn.example.com",
            GatewayPort = 8443,
            Username = "testuser"
        };

        await service.SaveProfileAsync(profile);
        service.Profiles.Should().HaveCount(1);

        // Load in a new instance
        var service2 = CreateService();
        await service2.LoadAsync();

        service2.Profiles.Should().ContainSingle();
        service2.Profiles[0].Name.Should().Be("Test VPN");
        service2.Profiles[0].GatewayHost.Should().Be("vpn.example.com");
        service2.Profiles[0].GatewayPort.Should().Be(8443);
        service2.Profiles[0].Username.Should().Be("testuser");
    }

    [Fact]
    public async Task DeleteProfile_RemovesFromList()
    {
        var service = CreateService();
        var profile = new VpnProfile { Name = "ToDelete" };
        await service.SaveProfileAsync(profile);
        service.Profiles.Should().HaveCount(1);

        await service.DeleteProfileAsync(profile.Id);
        service.Profiles.Should().BeEmpty();
    }

    [Fact]
    public void DuplicateProfile_CreatesNewIdAndName()
    {
        var service = CreateService();
        var original = new VpnProfile
        {
            Name = "Office VPN",
            GatewayHost = "vpn.example.com",
            GatewayPort = 443,
            Username = "john",
            SetRoutes = false,
            InsecureSsl = true,
            TrustedCertDigests = new List<string> { "aabb" }
        };

        var copy = service.DuplicateProfile(original);

        copy.Id.Should().NotBe(original.Id);
        copy.Name.Should().Be("Office VPN (Copy)");
        copy.GatewayHost.Should().Be(original.GatewayHost);
        copy.Username.Should().Be(original.Username);
        copy.SetRoutes.Should().Be(original.SetRoutes);
        copy.InsecureSsl.Should().Be(original.InsecureSsl);
        copy.TrustedCertDigests.Should().BeEquivalentTo(
            original.TrustedCertDigests);
        copy.LastConnectedAt.Should().BeNull();
    }

    [Fact]
    public async Task SaveProfile_Update_OverwritesExisting()
    {
        var service = CreateService();
        var profile = new VpnProfile
        {
            Name = "Original",
            GatewayHost = "old.example.com"
        };
        await service.SaveProfileAsync(profile);

        profile.Name = "Updated";
        profile.GatewayHost = "new.example.com";
        await service.SaveProfileAsync(profile);

        service.Profiles.Should().HaveCount(1);
        service.Profiles[0].Name.Should().Be("Updated");
        service.Profiles[0].GatewayHost.Should().Be("new.example.com");
    }
}
