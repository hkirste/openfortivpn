using OpenFortiVPN.GUI.Models;

namespace OpenFortiVPN.GUI.Services;

/// <summary>
/// Core service managing the openfortivpn process lifecycle.
/// Translates CLI output into structured events for the UI layer.
/// </summary>
public interface IVpnService
{
    /// <summary>Current connection state.</summary>
    ConnectionState State { get; }

    /// <summary>Live connection details (IP, DNS, interface, etc.).</summary>
    ConnectionInfo CurrentConnection { get; }

    /// <summary>Fired on every state transition.</summary>
    event EventHandler<ConnectionState> StateChanged;

    /// <summary>Fired when a new log line is parsed from CLI output.</summary>
    event EventHandler<LogEntry> LogReceived;

    /// <summary>Fired when the CLI requests OTP input from the user.</summary>
    event EventHandler OtpRequired;

    /// <summary>Fired when SAML browser login is needed.</summary>
    event EventHandler<string> SamlLoginRequired;

    /// <summary>Start a VPN connection with the given profile and password.</summary>
    Task ConnectAsync(VpnProfile profile, string? password, CancellationToken ct = default);

    /// <summary>Send an OTP token to the running CLI process.</summary>
    Task SubmitOtpAsync(string otp);

    /// <summary>Gracefully disconnect the active VPN session.</summary>
    Task DisconnectAsync();

    /// <summary>Whether a connection or connection attempt is active.</summary>
    bool IsActive { get; }

    /// <summary>Buffered log entries from the current/last session.</summary>
    IReadOnlyList<LogEntry> LogBuffer { get; }
}
