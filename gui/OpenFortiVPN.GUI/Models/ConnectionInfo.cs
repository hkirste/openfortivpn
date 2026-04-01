namespace OpenFortiVPN.GUI.Models;

/// <summary>
/// Real-time connection information updated during an active VPN session.
/// </summary>
public class ConnectionInfo
{
    public ConnectionState State { get; set; } = ConnectionState.Disconnected;
    public string? AssignedIp { get; set; }
    public string? GatewayIp { get; set; }
    public string? Dns1 { get; set; }
    public string? Dns2 { get; set; }
    public string? DnsSuffix { get; set; }
    public string? TunnelInterface { get; set; }
    public DateTime? ConnectedSince { get; set; }
    public string? ErrorMessage { get; set; }
    public ErrorCategory? ErrorCategory { get; set; }
    public int ReconnectAttempts { get; set; }
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Authenticating,
    WaitingForOtp,
    WaitingForSaml,
    NegotiatingTunnel,
    Connected,
    Reconnecting,
    Disconnecting,
    Error
}

public enum ErrorCategory
{
    NetworkUnreachable,
    DnsResolutionFailed,
    AuthenticationFailed,
    CertificateError,
    TunnelSetupFailed,
    PermissionDenied,
    ConfigurationError,
    ProcessNotFound,
    ProcessCrashed,
    Timeout,
    Unknown
}

/// <summary>
/// Maps CLI error patterns to user-friendly categories and messages.
/// </summary>
public static class ErrorClassifier
{
    private static readonly (string Pattern, ErrorCategory Category, string UserMessage)[] ErrorPatterns =
    {
        ("Could not resolve host",
            ErrorCategory.DnsResolutionFailed,
            "Cannot find the VPN server. Check the server address or your internet connection."),

        ("Could not connect to gateway",
            ErrorCategory.NetworkUnreachable,
            "Cannot reach the VPN server. Check your internet connection and firewall settings."),

        ("authentication failed",
            ErrorCategory.AuthenticationFailed,
            "Login failed. Please verify your username and password."),

        ("permission denied",
            ErrorCategory.AuthenticationFailed,
            "Access denied by the VPN server. Your account may be locked or unauthorized."),

        ("certificate verify failed",
            ErrorCategory.CertificateError,
            "The server's security certificate could not be verified. Contact your IT administrator."),

        ("X509 certificate",
            ErrorCategory.CertificateError,
            "There is a problem with the security certificate. It may be expired or untrusted."),

        ("pppd",
            ErrorCategory.TunnelSetupFailed,
            "The VPN tunnel could not be established. This may be a server-side issue."),

        ("Operation not permitted",
            ErrorCategory.PermissionDenied,
            "Administrator privileges are required to create the VPN tunnel."),

        ("No such file",
            ErrorCategory.ProcessNotFound,
            "The openfortivpn program was not found. Please check the installation."),
    };

    public static (ErrorCategory Category, string UserMessage) Classify(string rawError)
    {
        foreach (var (pattern, category, message) in ErrorPatterns)
        {
            if (rawError.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return (category, message);
            }
        }

        return (ErrorCategory.Unknown,
            "An unexpected error occurred. Check the logs for details.");
    }
}
