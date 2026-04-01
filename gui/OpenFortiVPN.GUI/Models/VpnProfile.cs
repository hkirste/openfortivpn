using System.Text.Json.Serialization;

namespace OpenFortiVPN.GUI.Models;

/// <summary>
/// Represents a saved VPN connection profile with all configuration needed
/// to establish a FortiGate SSL VPN tunnel via openfortivpn.
/// </summary>
public class VpnProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Connection";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastConnectedAt { get; set; }

    // --- Connection ---
    public string GatewayHost { get; set; } = string.Empty;
    public int GatewayPort { get; set; } = 443;
    public string Username { get; set; } = string.Empty;
    public string? Realm { get; set; }
    public string? SniOverride { get; set; }

    // --- Authentication ---
    public AuthMethod AuthMethod { get; set; } = AuthMethod.UsernamePassword;
    public string? UserCertPath { get; set; }
    public string? UserKeyPath { get; set; }
    public string? CaFilePath { get; set; }
    public List<string> TrustedCertDigests { get; set; } = new();
    public bool UseSaml { get; set; }
    public int SamlPort { get; set; } = 8020;

    // --- Network ---
    public bool SetRoutes { get; set; } = true;
    public bool SetDns { get; set; } = true;
    public bool HalfInternetRoutes { get; set; }

    // --- TLS / Security ---
    public bool InsecureSsl { get; set; }
    public string? CipherList { get; set; }
    public TlsVersion MinTlsVersion { get; set; } = TlsVersion.Default;
    public bool SecurityLevel1 { get; set; }

    // --- Reconnection ---
    public int PersistentInterval { get; set; }

    /// <summary>
    /// Builds the CLI argument list for openfortivpn.exe.
    /// Passwords are NEVER included here — they are piped via stdin.
    /// </summary>
    public List<string> BuildArguments()
    {
        var args = new List<string>();

        // Positional: host:port
        if (!string.IsNullOrWhiteSpace(GatewayHost))
        {
            args.Add(GatewayPort != 443
                ? $"{GatewayHost}:{GatewayPort}"
                : GatewayHost);
        }

        if (!string.IsNullOrWhiteSpace(Username))
        {
            args.Add("-u");
            args.Add(Username);
        }

        if (!string.IsNullOrWhiteSpace(Realm))
        {
            args.Add($"--realm={Realm}");
        }

        if (!string.IsNullOrWhiteSpace(SniOverride))
        {
            args.Add($"--sni={SniOverride}");
        }

        // Authentication
        if (!string.IsNullOrWhiteSpace(UserCertPath))
        {
            args.Add($"--user-cert={UserCertPath}");
        }

        if (!string.IsNullOrWhiteSpace(UserKeyPath))
        {
            args.Add($"--user-key={UserKeyPath}");
        }

        if (!string.IsNullOrWhiteSpace(CaFilePath))
        {
            args.Add($"--ca-file={CaFilePath}");
        }

        foreach (var digest in TrustedCertDigests)
        {
            args.Add($"--trusted-cert={digest}");
        }

        if (UseSaml)
        {
            args.Add($"--saml-login={SamlPort}");
        }

        // Network
        if (!SetRoutes)
        {
            args.Add("--no-routes");
        }

        if (!SetDns)
        {
            args.Add("--no-dns");
        }

        if (HalfInternetRoutes)
        {
            args.Add("--half-internet-routes=1");
        }

        // TLS / Security
        if (InsecureSsl)
        {
            args.Add("--insecure-ssl");
        }

        if (!string.IsNullOrWhiteSpace(CipherList))
        {
            args.Add($"--cipher-list={CipherList}");
        }

        if (MinTlsVersion != TlsVersion.Default)
        {
            args.Add($"--min-tls={MinTlsVersion.ToCliValue()}");
        }

        if (SecurityLevel1)
        {
            args.Add("--seclevel-1");
        }

        // Reconnection
        if (PersistentInterval > 0)
        {
            args.Add($"--persistent={PersistentInterval}");
        }

        // Always verbose for GUI parsing
        args.Add("-v");

        return args;
    }
}

public enum AuthMethod
{
    UsernamePassword,
    Certificate,
    UsernameAndCertificate,
    Saml,
    Cookie
}

public enum TlsVersion
{
    Default,
    Tls10,
    Tls11,
    Tls12,
    Tls13
}

public static class TlsVersionExtensions
{
    public static string ToCliValue(this TlsVersion version) => version switch
    {
        TlsVersion.Tls10 => "1.0",
        TlsVersion.Tls11 => "1.1",
        TlsVersion.Tls12 => "1.2",
        TlsVersion.Tls13 => "1.3",
        _ => ""
    };

    public static string ToDisplayString(this TlsVersion version) => version switch
    {
        TlsVersion.Tls10 => "TLS 1.0",
        TlsVersion.Tls11 => "TLS 1.1",
        TlsVersion.Tls12 => "TLS 1.2",
        TlsVersion.Tls13 => "TLS 1.3",
        _ => "System Default"
    };
}
