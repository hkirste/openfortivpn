using FluentAssertions;
using OpenFortiVPN.GUI.Models;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class VpnLogLevelExtensionsTests
{
    [Fact]
    public void ToCliFlags_Quiet_ReturnsMinusQ()
    {
        VpnLogLevel.Quiet.ToCliFlags().Should().BeEquivalentTo(new[] { "-q" });
    }

    [Fact]
    public void ToCliFlags_Error_ReturnsEmpty()
    {
        VpnLogLevel.Error.ToCliFlags().Should().BeEmpty();
    }

    [Fact]
    public void ToCliFlags_Warning_ReturnsEmpty()
    {
        VpnLogLevel.Warning.ToCliFlags().Should().BeEmpty();
    }

    [Fact]
    public void ToCliFlags_Info_ReturnsSingleV()
    {
        VpnLogLevel.Info.ToCliFlags().Should().BeEquivalentTo(new[] { "-v" });
    }

    [Fact]
    public void ToCliFlags_Debug_ReturnsTwoVs()
    {
        VpnLogLevel.Debug.ToCliFlags().Should().BeEquivalentTo(new[] { "-v", "-v" });
    }

    [Fact]
    public void ToCliFlags_DebugDetails_ReturnsThreeVs()
    {
        VpnLogLevel.DebugDetails.ToCliFlags()
            .Should().BeEquivalentTo(new[] { "-v", "-v", "-v" });
    }
}
