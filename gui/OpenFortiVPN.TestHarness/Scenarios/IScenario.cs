namespace OpenFortiVPN.TestHarness.Scenarios;

/// <summary>
/// Represents a test scenario that simulates a specific openfortivpn behavior.
/// </summary>
public interface IScenario
{
    /// <summary>
    /// Runs the scenario, emitting events and log output with the given delay between steps.
    /// </summary>
    /// <param name="delayMs">Milliseconds to pause between events for realistic timing.</param>
    /// <param name="passwordEcho">Whether to echo the password back to stdout after reading it.</param>
    Task RunAsync(int delayMs, bool passwordEcho);

    /// <summary>
    /// The process exit code this scenario should produce.
    /// </summary>
    int ExitCode { get; }
}
