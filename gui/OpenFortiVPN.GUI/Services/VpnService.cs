using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenFortiVPN.GUI.Models;

namespace OpenFortiVPN.GUI.Services;

/// <summary>
/// Manages the openfortivpn.exe child process.
///
/// Two modes of operation:
/// 1. Structured mode: CLI supports --json-events → stderr has JSON events
/// 2. Legacy mode: CLI does not support --json-events → parse stdout log text
///
/// Legacy mode is used when the bundled CLI predates the --json-events flag.
/// Passwords are delivered exclusively via stdin pipe.
/// </summary>
public sealed class VpnService : IVpnService, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly ILogger<VpnService> _logger;
    private Process? _process;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();
    private bool _jsonEventsSupported = true;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public ConnectionInfo CurrentConnection { get; } = new();
    public bool IsActive => State is not (ConnectionState.Disconnected or ConnectionState.Error);

    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<LogEntry>? LogReceived;
    public event EventHandler? OtpRequired;
    public event EventHandler<string>? SamlLoginRequired;

    private readonly List<string> _errorLines = new();

    // Regex for legacy stdout parsing
    private static readonly Regex GatewayIpRx =
        new(@"Gateway IP:\s+([\d.]+)", RegexOptions.Compiled);
    private static readonly Regex AssignedIpRx =
        new(@"Assigned IP:\s+([\d.]+)", RegexOptions.Compiled);
    private static readonly Regex CertDigestRx =
        new(@"Certificate digest:\s+([a-f0-9]+)", RegexOptions.Compiled);

    public VpnService(ISettingsService settings, ILogger<VpnService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task ConnectAsync(VpnProfile profile, string? password,
                                   CancellationToken ct = default)
    {
        if (IsActive)
            throw new InvalidOperationException(
                "A VPN connection is already active.");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _errorLines.Clear();
        CurrentConnection.ErrorMessage = null;
        CurrentConnection.ErrorCategory = null;
        var appSettings = _settings.Current;

        var args = profile.BuildArguments();

        // Insert flags before the positional host:port (always last)
        int insertPos = args.Count > 0 && !args[^1].StartsWith('-')
            ? args.Count - 1
            : args.Count;

        // Only add --json-events if the CLI supports it
        if (_jsonEventsSupported)
            args.Insert(insertPos, "--json-events");

        foreach (var flag in appSettings.LogVerbosity.ToCliFlags())
        {
            var pos = args.Count > 0 && !args[^1].StartsWith('-')
                ? args.Count - 1 : args.Count;
            args.Insert(pos, flag);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = appSettings.OpenFortiVpnPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        _logger.LogInformation("Starting openfortivpn: {Args}",
                               string.Join(" ", args));
        TransitionTo(ConnectionState.Connecting);

        try
        {
            _process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            _process.Exited += OnProcessExited;
            _process.Start();

            if (!string.IsNullOrEmpty(password))
            {
                await _process.StandardInput.WriteLineAsync(password);
                await _process.StandardInput.FlushAsync();
            }

            _ = Task.Run(
                () => ReadStreamAsync(_process.StandardOutput, "stdout"),
                _cts.Token);
            _ = Task.Run(
                () => ReadStreamAsync(_process.StandardError, "stderr"),
                _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start openfortivpn process");
            CurrentConnection.ErrorMessage =
                $"Could not start openfortivpn: {ex.Message}";
            CurrentConnection.ErrorCategory =
                Models.ErrorCategory.ProcessNotFound;
            TransitionTo(ConnectionState.Error);
            throw;
        }
    }

    public async Task SubmitOtpAsync(string otp)
    {
        if (_process is { HasExited: false })
        {
            await _process.StandardInput.WriteLineAsync(otp);
            await _process.StandardInput.FlushAsync();
            TransitionTo(ConnectionState.Authenticating);
        }
    }

    public Task DisconnectAsync()
    {
        lock (_lock)
        {
            if (_process is null or { HasExited: true })
            {
                TransitionTo(ConnectionState.Disconnected);
                return Task.CompletedTask;
            }

            TransitionTo(ConnectionState.Disconnecting);
        }

        try
        {
            _process.StandardInput.Close();
            if (!_process.WaitForExit(5000))
            {
                _logger.LogWarning(
                    "openfortivpn did not exit gracefully, killing");
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
            try { _process?.Kill(entireProcessTree: true); } catch { }
        }

        TransitionTo(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    private async Task ReadStreamAsync(System.IO.StreamReader reader,
                                       string source)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (source == "stderr")
                    HandleStderrLine(line);
                else
                    HandleStdoutLine(line);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stream {Source} read ended", source);
        }
    }

    /// <summary>
    /// Handle a line from stderr. Try JSON event parsing first;
    /// if it fails, check if the CLI rejected --json-events and
    /// retry without it.
    /// </summary>
    private void HandleStderrLine(string line)
    {
        // Try structured JSON event
        var evt = EventStreamParser.Parse(line);
        if (evt is not null)
        {
            HandleEvent(evt);
            return;
        }

        // Detect --json-events rejection → switch to legacy mode
        if (line.Contains("unrecognized option") &&
            line.Contains("json-events"))
        {
            _logger.LogWarning(
                "CLI does not support --json-events, using legacy mode");
            _jsonEventsSupported = false;
            return;
        }

        _logger.LogDebug("stderr: {Line}", line);
    }

    /// <summary>
    /// Handle a line from stdout. Always emit as a log entry.
    /// In legacy mode (no --json-events), also infer state transitions
    /// from the log prefix format (INFO:/ERROR:/WARN:/DEBUG:).
    /// </summary>
    private void HandleStdoutLine(string line)
    {
        var severity = ClassifyStdoutSeverity(line);
        var msg = StripPrefix(line);

        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Severity = severity,
            Message = msg,
            RawLine = line,
            Source = "stdout"
        };

        if (severity == LogSeverity.Error)
            _errorLines.Add(msg);

        LogReceived?.Invoke(this, entry);

        // Legacy mode: infer state from log messages
        if (!_jsonEventsSupported)
            InferStateFromLog(msg, severity);
    }

    /// <summary>
    /// Legacy fallback: infer connection state from stdout log text.
    /// Uses the structured prefix format (not English content) where
    /// possible, and known fixed strings from the CLI source code.
    /// </summary>
    private void InferStateFromLog(string msg, LogSeverity severity)
    {
        // Gateway IP resolved
        var gwMatch = GatewayIpRx.Match(msg);
        if (gwMatch.Success)
        {
            CurrentConnection.GatewayIp = gwMatch.Groups[1].Value;
            return;
        }

        // Assigned IP (tunnel is up)
        var ipMatch = AssignedIpRx.Match(msg);
        if (ipMatch.Success)
        {
            CurrentConnection.AssignedIp = ipMatch.Groups[1].Value;
            CurrentConnection.ConnectedSince = DateTime.Now;
            TransitionTo(ConnectionState.Connected);
            return;
        }

        // Certificate digest (cert error)
        var certMatch = CertDigestRx.Match(msg);
        if (certMatch.Success)
        {
            CurrentConnection.ErrorMessage =
                $"Server certificate not trusted.\n\n" +
                $"Certificate digest:\n{certMatch.Groups[1].Value}\n\n" +
                "Add this digest to your profile under " +
                "TLS / Security > Trusted Certificate Digests.";
            CurrentConnection.ErrorCategory =
                Models.ErrorCategory.CertificateError;
            return;
        }

        // State transitions from known log strings
        if (msg.StartsWith("Connected to gateway"))
            TransitionTo(ConnectionState.Authenticating);
        else if (msg.StartsWith("Authenticated"))
            TransitionTo(ConnectionState.NegotiatingTunnel);
        else if (msg.StartsWith("Tunnel interface is UP"))
        {
            CurrentConnection.ConnectedSince = DateTime.Now;
            TransitionTo(ConnectionState.Connected);
        }
        else if (msg.StartsWith("Tunnel interface is DOWN"))
            TransitionTo(ConnectionState.Reconnecting);
    }

    private static string StripPrefix(string line)
    {
        // Strip "ERROR:  ", "WARN:   ", "INFO:   ", "DEBUG:  " prefixes
        if (line.Length > 8 && line[4..8].TrimStart() == "" &&
            (line.StartsWith("ERRO") || line.StartsWith("WARN") ||
             line.StartsWith("INFO") || line.StartsWith("DEBU")))
        {
            int i = line.IndexOf(':');
            if (i > 0 && i < 8)
                return line[(i + 1)..].TrimStart();
        }

        return line.TrimStart();
    }

    private static LogSeverity ClassifyStdoutSeverity(string line)
    {
        if (line.StartsWith("ERROR:", StringComparison.Ordinal))
            return LogSeverity.Error;
        if (line.StartsWith("WARN:", StringComparison.Ordinal))
            return LogSeverity.Warning;
        if (line.StartsWith("DEBUG:", StringComparison.Ordinal))
            return LogSeverity.Debug;
        return LogSeverity.Info;
    }

    // --- Structured event handlers (--json-events mode) ---

    private void HandleEvent(VpnEvent evt)
    {
        _logger.LogDebug("Event: {Type} (seq {Seq})",
                         evt.EventType, evt.Sequence);

        switch (evt)
        {
            case StateChangeEvent sc:
                HandleStateChange(sc.State);
                break;
            case GatewayResolvedEvent gw:
                CurrentConnection.GatewayIp = gw.Ip;
                break;
            case CertErrorEvent cert:
                CurrentConnection.ErrorMessage =
                    $"Server certificate not trusted.\n\n" +
                    $"Certificate digest:\n{cert.Digest}\n\n" +
                    "Add this digest to your profile under " +
                    "TLS / Security > Trusted Certificate Digests.";
                CurrentConnection.ErrorCategory =
                    Models.ErrorCategory.CertificateError;
                break;
            case OtpRequiredEvent:
                OtpRequired?.Invoke(this, EventArgs.Empty);
                break;
            case SamlRequiredEvent saml:
                SamlLoginRequired?.Invoke(this, saml.Url);
                break;
            case ConfigReceivedEvent cfg:
                CurrentConnection.Dns1 = cfg.Dns1;
                CurrentConnection.Dns2 = cfg.Dns2;
                CurrentConnection.DnsSuffix = cfg.DnsSuffix;
                break;
            case TunnelUpEvent tu:
                CurrentConnection.AssignedIp = tu.LocalIp;
                CurrentConnection.Dns1 = tu.Dns1;
                CurrentConnection.Dns2 = tu.Dns2;
                CurrentConnection.ConnectedSince = DateTime.Now;
                TransitionTo(ConnectionState.Connected);
                break;
            case TunnelDownEvent:
                if (State == ConnectionState.Connected)
                {
                    CurrentConnection.ReconnectAttempts++;
                    TransitionTo(ConnectionState.Reconnecting);
                }
                else
                {
                    TransitionTo(ConnectionState.Disconnected);
                }
                break;
            case VpnErrorEvent err:
                CurrentConnection.ErrorMessage = err.Message;
                CurrentConnection.ErrorCategory =
                    MapErrorCategory(err.Category);
                break;
            case DisconnectedEvent:
                TransitionTo(ConnectionState.Disconnected);
                break;
        }
    }

    private void HandleStateChange(string state)
    {
        switch (state)
        {
            case "resolving":
            case "connecting_tls":
                TransitionTo(ConnectionState.Connecting);
                break;
            case "authenticating":
            case "allocating":
                TransitionTo(ConnectionState.Authenticating);
                break;
            case "waiting_otp":
                TransitionTo(ConnectionState.WaitingForOtp);
                OtpRequired?.Invoke(this, EventArgs.Empty);
                break;
            case "waiting_saml":
                TransitionTo(ConnectionState.WaitingForSaml);
                break;
            case "configuring":
            case "creating_adapter":
            case "tunneling":
                TransitionTo(ConnectionState.NegotiatingTunnel);
                break;
            case "connected":
                CurrentConnection.ConnectedSince = DateTime.Now;
                TransitionTo(ConnectionState.Connected);
                break;
            case "disconnecting":
                TransitionTo(ConnectionState.Disconnecting);
                break;
            case "disconnected":
                TransitionTo(ConnectionState.Disconnected);
                break;
        }
    }

    internal static Models.ErrorCategory MapErrorCategory(
        string category) => category switch
    {
        "network_unreachable" => Models.ErrorCategory.NetworkUnreachable,
        "dns_resolution_failed" => Models.ErrorCategory.DnsResolutionFailed,
        "authentication_failed" => Models.ErrorCategory.AuthenticationFailed,
        "certificate_error" => Models.ErrorCategory.CertificateError,
        "tunnel_setup_failed" => Models.ErrorCategory.TunnelSetupFailed,
        "permission_denied" => Models.ErrorCategory.PermissionDenied,
        "configuration_error" => Models.ErrorCategory.ConfigurationError,
        "timeout" => Models.ErrorCategory.Timeout,
        _ => Models.ErrorCategory.Unknown,
    };

    internal static (Models.ErrorCategory Category, string Message)?
        MapExitCode(int exitCode) => exitCode switch
    {
        0 => null,
        10 => (Models.ErrorCategory.DnsResolutionFailed,
            "Cannot find the VPN server."),
        11 => (Models.ErrorCategory.NetworkUnreachable,
            "Cannot reach the VPN server."),
        12 or 13 => (Models.ErrorCategory.CertificateError,
            "Server certificate verification failed."),
        20 => (Models.ErrorCategory.AuthenticationFailed,
            "Login failed. Please verify your username and password."),
        50 => (Models.ErrorCategory.PermissionDenied,
            "Administrator privileges are required."),
        _ => (Models.ErrorCategory.ProcessCrashed,
            $"VPN process exited unexpectedly (code {exitCode})."),
    };

    private void TransitionTo(ConnectionState newState)
    {
        if (State == newState) return;

        _logger.LogDebug("VPN state: {Old} → {New}", State, newState);
        State = newState;
        CurrentConnection.State = newState;
        StateChanged?.Invoke(this, newState);
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_process is null) return;

        var exitCode = _process.ExitCode;
        _logger.LogInformation(
            "openfortivpn exited with code {ExitCode}", exitCode);

        if (State is ConnectionState.Disconnecting
            or ConnectionState.Disconnected)
        {
            TransitionTo(ConnectionState.Disconnected);
            return;
        }

        var mapped = MapExitCode(exitCode);
        if (mapped is null)
        {
            TransitionTo(ConnectionState.Disconnected);
            return;
        }

        SetExitError(mapped.Value.Category, mapped.Value.Message);

        // Use collected stdout ERROR lines if more specific
        if (_errorLines.Count > 0 &&
            CurrentConnection.ErrorMessage is not null &&
            CurrentConnection.ErrorMessage.Contains("(code "))
        {
            CurrentConnection.ErrorMessage =
                string.Join("\n", _errorLines);
        }

        if (State != ConnectionState.Error)
            TransitionTo(ConnectionState.Error);
    }

    private void SetExitError(Models.ErrorCategory category, string message)
    {
        if (CurrentConnection.ErrorCategory is null)
            CurrentConnection.ErrorCategory = category;
        if (string.IsNullOrEmpty(CurrentConnection.ErrorMessage))
            CurrentConnection.ErrorMessage = message;
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _cts?.Dispose(); } catch { }
        try
        {
            if (_process is not null && !_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch { }
        try { _process?.Dispose(); } catch { }
    }
}
