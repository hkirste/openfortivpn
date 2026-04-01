namespace OpenFortiVPN.TestHarness.Scenarios;

/// <summary>
/// Authentication failure: the user provides incorrect credentials.
/// </summary>
public sealed class AuthFailedScenario : IScenario
{
    public int ExitCode => 20;

    public Task RunAsync(int delayMs, bool passwordEcho)
    {
        Program.Log("INFO", "Starting openfortivpn-harness (scenario: auth_failed)");

        // Read password from stdin
        Program.Log("INFO", "Waiting for password on stdin...");
        var password = Console.ReadLine();
        if (passwordEcho)
            Program.Log("DEBUG", $"Password received ({password?.Length ?? 0} chars)");
        else
            Program.Log("DEBUG", "Password received");

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

        // Authenticating
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Authenticating with gateway...");
        Program.EmitEvent("state_change", new Dictionary<string, object>
        {
            ["state"] = "authenticating"
        });

        // Authentication failed
        Thread.Sleep(delayMs);
        Program.Log("ERROR", "Authentication failed: invalid credentials.");
        Program.EmitEvent("error", new Dictionary<string, object>
        {
            ["code"] = 20,
            ["category"] = "authentication_failed"
        });

        Program.Log("ERROR", "Exiting with error code 20 (authentication_failed).");
        return Task.CompletedTask;
    }
}
