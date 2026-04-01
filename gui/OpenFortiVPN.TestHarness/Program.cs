using System.Text.Json;
using OpenFortiVPN.TestHarness.Scenarios;

namespace OpenFortiVPN.TestHarness;

public static class Program
{
    private static int _sequenceNumber;

    public static async Task<int> Main(string[] args)
    {
        var scenario = Environment.GetEnvironmentVariable("SCENARIO")
            ?? (args.Length > 0 ? args[0] : null);

        if (string.IsNullOrEmpty(scenario))
        {
            Console.Error.WriteLine("Usage: openfortivpn-harness <scenario>");
            Console.Error.WriteLine("  or set SCENARIO environment variable");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Available scenarios:");
            Console.Error.WriteLine("  successful_connect   Full happy-path connection");
            Console.Error.WriteLine("  cert_error           Certificate verification failure");
            Console.Error.WriteLine("  auth_failed          Wrong password");
            Console.Error.WriteLine("  dns_failed           DNS resolution failure");
            Console.Error.WriteLine("  otp_required         Two-factor authentication");
            Console.Error.WriteLine("  permission_denied    Not running as admin");
            return 1;
        }

        var delayMs = int.TryParse(
            Environment.GetEnvironmentVariable("DELAY_MS"), out var d) ? d : 50;

        var passwordEcho = Environment.GetEnvironmentVariable("PASSWORD_ECHO") == "1";

        IScenario instance = scenario.ToLowerInvariant() switch
        {
            "successful_connect" => new SuccessfulConnectScenario(),
            "cert_error" => new CertErrorScenario(),
            "auth_failed" => new AuthFailedScenario(),
            "dns_failed" => new DnsFailedScenario(),
            "otp_required" => new OtpRequiredScenario(),
            "permission_denied" => new PermissionDeniedScenario(),
            _ => throw new ArgumentException($"Unknown scenario: {scenario}")
        };

        await instance.RunAsync(delayMs, passwordEcho);
        return instance.ExitCode;
    }

    /// <summary>
    /// Writes a structured JSON event to stderr with event type, timestamp,
    /// and an incrementing sequence number.
    /// </summary>
    public static void EmitEvent(string eventType, Dictionary<string, object>? data = null)
    {
        var seq = Interlocked.Increment(ref _sequenceNumber);

        var envelope = new Dictionary<string, object>
        {
            ["event"] = eventType,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
            ["seq"] = seq
        };

        if (data is not null)
        {
            foreach (var kvp in data)
            {
                envelope[kvp.Key] = kvp.Value;
            }
        }

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        Console.Error.WriteLine(json);
        Console.Error.Flush();
    }

    /// <summary>
    /// Writes a human-readable log line to stdout matching openfortivpn format.
    /// </summary>
    public static void Log(string level, string message)
    {
        // openfortivpn uses fixed-width level prefixes: "INFO:   ", "ERROR:  ", "WARN:   "
        var prefix = level.ToUpperInvariant() switch
        {
            "INFO" => "INFO:   ",
            "ERROR" => "ERROR:  ",
            "WARN" => "WARN:   ",
            "DEBUG" => "DEBUG:  ",
            _ => $"{level}:  "
        };

        Console.WriteLine($"{prefix}{message}");
        Console.Out.Flush();
    }
}
