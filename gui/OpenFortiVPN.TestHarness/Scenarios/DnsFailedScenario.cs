namespace OpenFortiVPN.TestHarness.Scenarios;

/// <summary>
/// DNS resolution failure: the gateway hostname cannot be resolved.
/// </summary>
public sealed class DnsFailedScenario : IScenario
{
    public int ExitCode => 10;

    public Task RunAsync(int delayMs, bool passwordEcho)
    {
        Program.Log("INFO", "Starting openfortivpn-harness (scenario: dns_failed)");

        // Resolving
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Resolving gateway address...");
        Program.EmitEvent("state_change", new Dictionary<string, object>
        {
            ["state"] = "resolving"
        });

        // DNS failure
        Thread.Sleep(delayMs);
        Program.Log("ERROR", "Could not resolve gateway hostname: vpn.example.com");
        Program.EmitEvent("error", new Dictionary<string, object>
        {
            ["code"] = 10,
            ["category"] = "dns_resolution_failed"
        });

        Program.Log("ERROR", "Exiting with error code 10 (dns_resolution_failed).");
        return Task.CompletedTask;
    }
}
