namespace OpenFortiVPN.TestHarness.Scenarios;

/// <summary>
/// Two-factor authentication: after primary authentication the gateway
/// requests a one-time password, then connection completes normally.
/// </summary>
public sealed class OtpRequiredScenario : IScenario
{
    public int ExitCode => 0;

    public async Task RunAsync(int delayMs, bool passwordEcho)
    {
        Program.Log("INFO", "Starting openfortivpn-harness (scenario: otp_required)");

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

        // OTP required
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Two-factor authentication required. Waiting for OTP on stdin...");
        Program.EmitEvent("state_change", new Dictionary<string, object>
        {
            ["state"] = "waiting_otp"
        });

        // Read OTP from stdin
        var otp = Console.ReadLine();
        Program.Log("DEBUG", $"OTP received ({otp?.Length ?? 0} chars)");

        // Allocating
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Allocating VPN resources...");
        Program.EmitEvent("state_change", new Dictionary<string, object>
        {
            ["state"] = "allocating"
        });

        // Configuring
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Configuring network interface...");
        Program.EmitEvent("state_change", new Dictionary<string, object>
        {
            ["state"] = "configuring"
        });

        // Creating adapter
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Creating PPP adapter...");
        Program.EmitEvent("state_change", new Dictionary<string, object>
        {
            ["state"] = "creating_adapter"
        });

        // Tunnel up
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Tunnel is up and running.");
        Program.EmitEvent("tunnel_up", new Dictionary<string, object>
        {
            ["local_ip"] = "10.211.1.42",
            ["dns1"] = "10.211.1.1",
            ["dns2"] = "10.211.1.2"
        });

        // Connected
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Connected to VPN.");
        Program.EmitEvent("state_change", new Dictionary<string, object>
        {
            ["state"] = "connected"
        });

        // Wait for stdin to close (user disconnect signal)
        Program.Log("INFO", "VPN active. Waiting for disconnect signal (stdin close)...");
        await WaitForStdinCloseAsync();

        // Disconnecting
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Disconnecting from VPN...");
        Program.EmitEvent("state_change", new Dictionary<string, object>
        {
            ["state"] = "disconnecting"
        });

        // Disconnected
        Thread.Sleep(delayMs);
        Program.Log("INFO", "Disconnected.");
        Program.EmitEvent("state_change", new Dictionary<string, object>
        {
            ["state"] = "disconnected"
        });
    }

    private static async Task WaitForStdinCloseAsync()
    {
        try
        {
            while (true)
            {
                var line = await Task.Run(() => Console.ReadLine());
                if (line is null)
                    break;
            }
        }
        catch (IOException)
        {
            // stdin closed
        }
    }
}
