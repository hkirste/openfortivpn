using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenFortiVPN.GUI.Models;

namespace OpenFortiVPN.GUI.Services;

/// <summary>
/// Manages the openfortivpn.exe child process. Structured JSON events on
/// stderr drive the state machine; stdout is forwarded to the log viewer.
/// Passwords are delivered exclusively via stdin pipe.
/// </summary>
public sealed class VpnService : IVpnService, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly ILogger<VpnService> _logger;
    private Process? _process;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public ConnectionInfo CurrentConnection { get; } = new();
    public bool IsActive => State is not (ConnectionState.Disconnected or ConnectionState.Error);

    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<LogEntry>? LogReceived;
    public event EventHandler? OtpRequired;
    public event EventHandler<string>? SamlLoginRequired;

    // Collect ERROR lines from stdout as fallback for error display
    private readonly List<string> _errorLines = new();

    public VpnService(ISettingsService settings, ILogger<VpnService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task ConnectAsync(VpnProfile profile, string? password, CancellationToken ct = default)
    {
        if (IsActive)
        {
            throw new InvalidOperationException("A VPN connection is already active.");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _errorLines.Clear();
        var appSettings = _settings.Current;

        var args = profile.BuildArguments();

        // Insert --json-events before verbosity flags.
        // host:port is last in the list (Windows getopt quirk), so insert
        // just before it so the positional argument stays at the end.
        int insertPos = args.Count > 0 && !args[^1].StartsWith('-')
            ? args.Count - 1
            : args.Count;
        args.Insert(insertPos, "--json-events");

        // Add verbosity flags from settings (these go before the positional arg too)
        foreach (var flag in appSettings.LogVerbosity.ToCliFlags())
        {
            args.Insert(insertPos + 1, flag);
            insertPos++;
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
        {
            startInfo.ArgumentList.Add(arg);
        }

        _logger.LogInformation("Starting openfortivpn: {Args}", string.Join(" ", args));
        TransitionTo(ConnectionState.Connecting);

        try
        {
            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _process.Exited += OnProcessExited;
            _process.Start();

            // Send password via stdin (never as CLI argument)
            if (!string.IsNullOrEmpty(password))
            {
                await _process.StandardInput.WriteLineAsync(password);
                await _process.StandardInput.FlushAsync();
            }

            // stdout → log viewer (human-readable lines)
            _ = Task.Run(() => ReadStreamAsync(_process.StandardOutput, "stdout"), _cts.Token);

            // stderr → structured JSON event stream
            _ = Task.Run(() => ReadStreamAsync(_process.StandardError, "stderr"), _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start openfortivpn process");
            CurrentConnection.ErrorMessage = $"Could not start openfortivpn: {ex.Message}";
            CurrentConnection.ErrorCategory = Models.ErrorCategory.ProcessNotFound;
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
            // Attempt graceful termination via Ctrl+C signal
            // On Windows, we close stdin which causes openfortivpn to exit
            _process.StandardInput.Close();

            // Give it a moment to clean up routes/DNS
            if (!_process.WaitForExit(5000))
            {
                _logger.LogWarning("openfortivpn did not exit gracefully, killing");
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
            try { _process?.Kill(entireProcessTree: true); } catch { /* best effort */ }
        }

        TransitionTo(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    private async Task ReadStreamAsync(System.IO.StreamReader reader, string source)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (source == "stderr")
                {
                    // Structured JSON event stream
                    var evt = EventStreamParser.Parse(line);
                    if (evt is not null)
                    {
                        HandleEvent(evt);
                    }
                    else
                    {
                        _logger.LogDebug("Non-JSON stderr line: {Line}", line);
                    }
                }
                else
                {
                    // stdout: human-readable log lines for the log viewer
                    var severity = ClassifyStdoutSeverity(line);

                    var entry = new LogEntry
                    {
                        Timestamp = DateTime.Now,
                        Severity = severity,
                        Message = line.TrimStart(),
                        RawLine = line,
                        Source = source
                    };

                    if (severity == LogSeverity.Error)
                        _errorLines.Add(line.TrimStart());

                    LogReceived?.Invoke(this, entry);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stream {Source} read ended", source);
        }
    }

    /// <summary>
    /// Classify stdout line severity by prefix.
    /// </summary>
    private static LogSeverity ClassifyStdoutSeverity(string line)
    {
        if (line.StartsWith("ERROR:", StringComparison.Ordinal)) return LogSeverity.Error;
        if (line.StartsWith("WARN:", StringComparison.Ordinal)) return LogSeverity.Warning;
        if (line.StartsWith("DEBUG:", StringComparison.Ordinal)) return LogSeverity.Debug;
        return LogSeverity.Info;
    }

    /// <summary>
    /// Central event handler that drives the state machine from structured
    /// JSON events emitted on stderr by openfortivpn --json-events.
    /// </summary>
    private void HandleEvent(VpnEvent evt)
    {
        _logger.LogDebug("Event: {EventType} (seq {Seq})", evt.EventType, evt.Sequence);

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
                    $"Add this digest to your profile under TLS / Security > Trusted Certificate Digests.";
                CurrentConnection.ErrorCategory = Models.ErrorCategory.CertificateError;
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
                // If persistent reconnect is configured, go to Reconnecting;
                // otherwise treat as disconnected.
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
                CurrentConnection.ErrorCategory = MapErrorCategory(err.Category);
                break;

            case DisconnectedEvent:
                TransitionTo(ConnectionState.Disconnected);
                break;
        }
    }

    /// <summary>
    /// Map a state_change state string to a ConnectionState and transition.
    /// </summary>
    private void HandleStateChange(string state)
    {
        switch (state)
        {
            case "resolving":
                TransitionTo(ConnectionState.Connecting);
                break;
            case "connecting_tls":
                TransitionTo(ConnectionState.Connecting);
                break;
            case "authenticating":
                TransitionTo(ConnectionState.Authenticating);
                break;
            case "waiting_otp":
                TransitionTo(ConnectionState.WaitingForOtp);
                OtpRequired?.Invoke(this, EventArgs.Empty);
                break;
            case "waiting_saml":
                TransitionTo(ConnectionState.WaitingForSaml);
                break;
            case "allocating":
                TransitionTo(ConnectionState.Authenticating);
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

    /// <summary>
    /// Map the error category string from a VpnErrorEvent to the ErrorCategory enum.
    /// </summary>
    internal static Models.ErrorCategory MapErrorCategory(string category) => category switch
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

    internal static (Models.ErrorCategory Category, string Message)? MapExitCode(
        int exitCode) => exitCode switch
    {
        0 => null,
        10 => (Models.ErrorCategory.DnsResolutionFailed,
            "Cannot find the VPN server. Check the server address or your internet connection."),
        11 => (Models.ErrorCategory.NetworkUnreachable,
            "Cannot reach the VPN server. Check your internet connection and firewall settings."),
        12 or 13 => (Models.ErrorCategory.CertificateError,
            "Server certificate verification failed. Add the certificate digest to your profile's trusted certificates."),
        20 => (Models.ErrorCategory.AuthenticationFailed,
            "Login failed. Please verify your username and password."),
        50 => (Models.ErrorCategory.PermissionDenied,
            "Administrator privileges are required to create the VPN tunnel."),
        _ => (Models.ErrorCategory.ProcessCrashed,
            $"VPN process exited unexpectedly (code {exitCode})."),
    };

    private void TransitionTo(ConnectionState newState)
    {
        if (State == newState) return;

        _logger.LogDebug("VPN state: {OldState} → {NewState}", State, newState);
        State = newState;
        CurrentConnection.State = newState;
        StateChanged?.Invoke(this, newState);
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_process is null) return;

        var exitCode = _process.ExitCode;
        _logger.LogInformation("openfortivpn exited with code {ExitCode}", exitCode);

        if (State == ConnectionState.Disconnecting || State == ConnectionState.Disconnected)
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

        // If we collected error lines from stdout and the error message is
        // still the generic exit-code-based one, replace with the real output.
        if (_errorLines.Count > 0 && CurrentConnection.ErrorMessage is not null
            && CurrentConnection.ErrorMessage.Contains("(code "))
        {
            CurrentConnection.ErrorMessage = string.Join("\n", _errorLines);
        }

        if (State != ConnectionState.Error)
        {
            TransitionTo(ConnectionState.Error);
        }
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
