using System.Text.Json;

namespace OpenFortiVPN.GUI.Services;

public record VpnEvent(string EventType, long Timestamp, long Sequence);

public record StateChangeEvent(string State, string EventType, long Timestamp, long Sequence)
    : VpnEvent(EventType, Timestamp, Sequence);

public record GatewayResolvedEvent(string Ip, string EventType, long Timestamp, long Sequence)
    : VpnEvent(EventType, Timestamp, Sequence);

public record CertErrorEvent(string Digest, string Reason, string EventType, long Timestamp, long Sequence)
    : VpnEvent(EventType, Timestamp, Sequence);

public record OtpRequiredEvent(string Prompt, string EventType, long Timestamp, long Sequence)
    : VpnEvent(EventType, Timestamp, Sequence);

public record SamlRequiredEvent(string Url, int Port, string EventType, long Timestamp, long Sequence)
    : VpnEvent(EventType, Timestamp, Sequence);

public record ConfigReceivedEvent(string Ip, string? Dns1, string? Dns2, string? DnsSuffix, string EventType, long Timestamp, long Sequence)
    : VpnEvent(EventType, Timestamp, Sequence);

public record TunnelUpEvent(string LocalIp, string? Dns1, string? Dns2, string EventType, long Timestamp, long Sequence)
    : VpnEvent(EventType, Timestamp, Sequence);

public record TunnelDownEvent(string Reason, string EventType, long Timestamp, long Sequence)
    : VpnEvent(EventType, Timestamp, Sequence);

public record VpnErrorEvent(int Code, string Category, string Message, string? Detail, string EventType, long Timestamp, long Sequence)
    : VpnEvent(EventType, Timestamp, Sequence);

public record DisconnectedEvent(int ExitCode, long DurationMs, string EventType, long Timestamp, long Sequence)
    : VpnEvent(EventType, Timestamp, Sequence);

public static class EventStreamParser
{
    public static VpnEvent? Parse(string jsonLine)
    {
        if (string.IsNullOrWhiteSpace(jsonLine))
            return null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonLine);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (!root.TryGetProperty("event", out var eventElement))
                return null;

            var eventType = eventElement.GetString();
            if (eventType is null)
                return null;

            var ts = root.TryGetProperty("ts", out var tsEl) ? tsEl.GetInt64() : 0;
            var seq = root.TryGetProperty("seq", out var seqEl) ? seqEl.GetInt64() : 0;

            return eventType switch
            {
                "state_change" => new StateChangeEvent(
                    GetStringOrDefault(root, "state"),
                    eventType, ts, seq),

                "gateway_resolved" => new GatewayResolvedEvent(
                    GetStringOrDefault(root, "ip"),
                    eventType, ts, seq),

                "cert_error" => new CertErrorEvent(
                    GetStringOrDefault(root, "digest"),
                    GetStringOrDefault(root, "reason"),
                    eventType, ts, seq),

                "otp_required" => new OtpRequiredEvent(
                    GetStringOrDefault(root, "prompt"),
                    eventType, ts, seq),

                "saml_required" => new SamlRequiredEvent(
                    GetStringOrDefault(root, "url"),
                    root.TryGetProperty("port", out var portEl) ? portEl.GetInt32() : 0,
                    eventType, ts, seq),

                "config_received" => new ConfigReceivedEvent(
                    GetStringOrDefault(root, "ip"),
                    GetStringOrDefault(root, "dns1", null!),
                    GetStringOrDefault(root, "dns2", null!),
                    GetStringOrDefault(root, "dns_suffix", null!),
                    eventType, ts, seq),

                "tunnel_up" => new TunnelUpEvent(
                    GetStringOrDefault(root, "local_ip"),
                    GetStringOrDefault(root, "dns1", null!),
                    GetStringOrDefault(root, "dns2", null!),
                    eventType, ts, seq),

                "tunnel_down" => new TunnelDownEvent(
                    GetStringOrDefault(root, "reason"),
                    eventType, ts, seq),

                "error" => new VpnErrorEvent(
                    root.TryGetProperty("code", out var codeEl) ? codeEl.GetInt32() : 0,
                    GetStringOrDefault(root, "category"),
                    GetStringOrDefault(root, "message"),
                    GetStringOrDefault(root, "detail", null!),
                    eventType, ts, seq),

                "disconnected" => new DisconnectedEvent(
                    root.TryGetProperty("exit_code", out var exitEl) ? exitEl.GetInt32() : 0,
                    root.TryGetProperty("duration_ms", out var durEl) ? durEl.GetInt64() : 0,
                    eventType, ts, seq),

                _ => new VpnEvent(eventType, ts, seq)
            };
        }
    }

    private static string GetStringOrDefault(JsonElement root, string property, string defaultValue = "")
    {
        if (root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? defaultValue;

        return defaultValue;
    }
}
