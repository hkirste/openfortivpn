namespace OpenFortiVPN.TestHarness.Scenarios;

/// <summary>
/// Certificate verification failure: the gateway presents an untrusted
/// or invalid TLS certificate.
/// </summary>
public sealed class CertErrorScenario : IScenario
{
    public int ExitCode => 13;

    public Task RunAsync(int delayMs, bool passwordEcho)
    {
        Program.Log("INFO", "Starting openfortivpn-harness (scenario: cert_error)");

        // Resolving
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Resolving gateway address...");
        Program.EmitEvent("state_change", new Dictionary<string, object>
        {
            ["state"] = "resolving"
        });

        // Gateway resolved
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Gateway resolved: vpn.example.com -> 203.0.113.1");
        Program.EmitEvent("gateway_resolved", new Dictionary<string, object>
        {
            ["hostname"] = "vpn.example.com",
            ["ip"] = "203.0.113.1"
        });

        // Connecting TLS
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Establishing TLS connection to 203.0.113.1:443...");
        Program.EmitEvent("state_change", new Dictionary<string, object>
        {
            ["state"] = "connecting_tls"
        });

        // Certificate error
        Thread.Sleep(delayMs);
        Program.Log("ERROR", "Server certificate verification failed.");
        Program.Log("ERROR", "Certificate digest: a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2");
        Program.Log("ERROR", "Reason: self-signed certificate in certificate chain");
        Program.EmitEvent("cert_error", new Dictionary<string, object>
        {
            ["digest"] = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
            ["reason"] = "self-signed certificate in certificate chain"
        });

        // Error event
        Thread.Sleep(delayMs);
        Program.EmitEvent("error", new Dictionary<string, object>
        {
            ["code"] = 13,
            ["category"] = "certificate_error"
        });

        Program.Log("ERROR", "Exiting with error code 13 (certificate_error).");
        return Task.CompletedTask;
    }
}
