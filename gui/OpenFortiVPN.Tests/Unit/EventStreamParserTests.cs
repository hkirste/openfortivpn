using FluentAssertions;
using OpenFortiVPN.GUI.Services;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class EventStreamParserTests
{
    [Fact]
    public void Parse_StateChangeEvent_ReturnsTypedRecord()
    {
        var json = """{"event":"state_change","state":"connecting","ts":1000,"seq":1}""";

        var result = EventStreamParser.Parse(json);

        result.Should().BeOfType<StateChangeEvent>();
        var e = (StateChangeEvent)result!;
        e.State.Should().Be("connecting");
        e.EventType.Should().Be("state_change");
        e.Timestamp.Should().Be(1000);
        e.Sequence.Should().Be(1);
    }

    [Fact]
    public void Parse_GatewayResolvedEvent_ExtractsIp()
    {
        var json = """{"event":"gateway_resolved","ip":"10.0.0.1","ts":2000,"seq":2}""";

        var result = EventStreamParser.Parse(json);

        result.Should().BeOfType<GatewayResolvedEvent>();
        var e = (GatewayResolvedEvent)result!;
        e.Ip.Should().Be("10.0.0.1");
        e.EventType.Should().Be("gateway_resolved");
        e.Timestamp.Should().Be(2000);
        e.Sequence.Should().Be(2);
    }

    [Fact]
    public void Parse_CertErrorEvent_ExtractsDigestAndReason()
    {
        var json = """{"event":"cert_error","digest":"aabb1122","reason":"self-signed certificate","ts":3000,"seq":3}""";

        var result = EventStreamParser.Parse(json);

        result.Should().BeOfType<CertErrorEvent>();
        var e = (CertErrorEvent)result!;
        e.Digest.Should().Be("aabb1122");
        e.Reason.Should().Be("self-signed certificate");
    }

    [Fact]
    public void Parse_TunnelUpEvent_ExtractsLocalIpAndDns()
    {
        var json = """{"event":"tunnel_up","local_ip":"192.168.1.100","dns1":"8.8.8.8","dns2":"8.8.4.4","ts":4000,"seq":4}""";

        var result = EventStreamParser.Parse(json);

        result.Should().BeOfType<TunnelUpEvent>();
        var e = (TunnelUpEvent)result!;
        e.LocalIp.Should().Be("192.168.1.100");
        e.Dns1.Should().Be("8.8.8.8");
        e.Dns2.Should().Be("8.8.4.4");
    }

    [Fact]
    public void Parse_TunnelDownEvent_ExtractsReason()
    {
        var json = """{"event":"tunnel_down","reason":"user requested","ts":5000,"seq":5}""";

        var result = EventStreamParser.Parse(json);

        result.Should().BeOfType<TunnelDownEvent>();
        var e = (TunnelDownEvent)result!;
        e.Reason.Should().Be("user requested");
    }

    [Fact]
    public void Parse_ErrorEvent_ExtractsAllFields()
    {
        var json = """{"event":"error","code":101,"category":"auth","message":"login failed","detail":"bad password","ts":6000,"seq":6}""";

        var result = EventStreamParser.Parse(json);

        result.Should().BeOfType<VpnErrorEvent>();
        var e = (VpnErrorEvent)result!;
        e.Code.Should().Be(101);
        e.Category.Should().Be("auth");
        e.Message.Should().Be("login failed");
        e.Detail.Should().Be("bad password");
    }

    [Fact]
    public void Parse_ErrorEvent_NullDetail()
    {
        var json = """{"event":"error","code":102,"category":"network","message":"timeout","ts":6500,"seq":7}""";

        var result = EventStreamParser.Parse(json);

        result.Should().BeOfType<VpnErrorEvent>();
        var e = (VpnErrorEvent)result!;
        e.Detail.Should().BeNull();
        e.Message.Should().Be("timeout");
    }

    [Fact]
    public void Parse_DisconnectedEvent_ExtractsExitCodeAndDuration()
    {
        var json = """{"event":"disconnected","exit_code":0,"duration_ms":120000,"ts":7000,"seq":8}""";

        var result = EventStreamParser.Parse(json);

        result.Should().BeOfType<DisconnectedEvent>();
        var e = (DisconnectedEvent)result!;
        e.ExitCode.Should().Be(0);
        e.DurationMs.Should().Be(120000);
    }

    [Fact]
    public void Parse_UnknownEventType_ReturnsBaseVpnEvent()
    {
        var json = """{"event":"some_future_event","ts":8000,"seq":9}""";

        var result = EventStreamParser.Parse(json);

        result.Should().NotBeNull();
        result.Should().BeOfType<VpnEvent>();
        result!.EventType.Should().Be("some_future_event");
        result.Timestamp.Should().Be(8000);
        result.Sequence.Should().Be(9);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsNull()
    {
        var json = "{ this is not valid json }}}";

        var result = EventStreamParser.Parse(json);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        var result = EventStreamParser.Parse("");

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
        var result = EventStreamParser.Parse(null!);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_MissingEventField_ReturnsNull()
    {
        var json = """{"state":"connecting","ts":1000,"seq":1}""";

        var result = EventStreamParser.Parse(json);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_ExtraFields_IgnoredGracefully()
    {
        var json = """{"event":"state_change","state":"connected","ts":9000,"seq":10,"extra_field":"should_be_ignored","another":42}""";

        var result = EventStreamParser.Parse(json);

        result.Should().BeOfType<StateChangeEvent>();
        var e = (StateChangeEvent)result!;
        e.State.Should().Be("connected");
        e.EventType.Should().Be("state_change");
    }
}
