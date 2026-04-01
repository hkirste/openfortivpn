namespace OpenFortiVPN.GUI.Models;

/// <summary>
/// Application-wide settings persisted to JSON.
/// </summary>
public class AppSettings
{
    // --- Startup ---
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool AutoConnectLastProfile { get; set; }
    public Guid? LastProfileId { get; set; }

    // --- UI ---
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool ShowConnectionDuration { get; set; } = true;

    // --- Logging ---
    public VpnLogLevel LogVerbosity { get; set; } = VpnLogLevel.Info;
    public int MaxLogRetentionDays { get; set; } = 30;
    public int MaxLogLines { get; set; } = 10_000;

    // --- CLI ---
    public string OpenFortiVpnPath { get; set; } = "openfortivpn.exe";

    // --- Window State (not user-configurable, auto-saved) ---
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 900;
    public double WindowHeight { get; set; } = 620;
}

public enum ThemeMode
{
    Light,
    Dark,
    System
}

public enum VpnLogLevel
{
    Quiet,
    Error,
    Warning,
    Info,
    Debug,
    DebugDetails
}

public static class VpnLogLevelExtensions
{
    /// <summary>
    /// Returns the CLI verbosity flags corresponding to this log level.
    /// Default (Info) = one -v. Debug = -v -v. Quiet = -q.
    /// </summary>
    public static IEnumerable<string> ToCliFlags(this VpnLogLevel level) => level switch
    {
        VpnLogLevel.Quiet => new[] { "-q" },
        VpnLogLevel.Error => Array.Empty<string>(),
        VpnLogLevel.Warning => Array.Empty<string>(),
        VpnLogLevel.Info => new[] { "-v" },
        VpnLogLevel.Debug => new[] { "-v", "-v" },
        VpnLogLevel.DebugDetails => new[] { "-v", "-v", "-v" },
        _ => new[] { "-v" }
    };
}
