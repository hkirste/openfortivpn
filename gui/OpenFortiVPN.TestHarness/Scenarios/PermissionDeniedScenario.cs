namespace OpenFortiVPN.TestHarness.Scenarios;

/// <summary>
/// Permission denied: the process is not running with administrator privileges.
/// </summary>
public sealed class PermissionDeniedScenario : IScenario
{
    public int ExitCode => 50;

    public Task RunAsync(int delayMs, bool passwordEcho)
    {
        Program.Log("INFO", "Starting openfortivpn-harness (scenario: permission_denied)");

        // Permission denied immediately
        Thread.Sleep(delayMs);
        Program.Log("ERROR", "This process requires administrator privileges.");
        Program.Log("ERROR", "Please run as administrator or use an elevated prompt.");
        Program.EmitEvent("error", new Dictionary<string, object>
        {
            ["code"] = 50,
            ["category"] = "permission_denied"
        });

        Program.Log("ERROR", "Exiting with error code 50 (permission_denied).");
        return Task.CompletedTask;
    }
}
