namespace OpenFortiVPN.GUI.Models;

/// <summary>
/// A single log entry captured from the openfortivpn process output.
/// </summary>
public record LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public LogSeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? RawLine { get; init; }
    public string? Source { get; init; } // "vpn", "app", "pppd"
}

public enum LogSeverity
{
    Debug,
    Info,
    Warning,
    Error
}
