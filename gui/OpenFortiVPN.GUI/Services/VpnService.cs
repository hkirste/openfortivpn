using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenFortiVPN.GUI.Models;

namespace OpenFortiVPN.GUI.Services;

/// <summary>
/// Manages the openfortivpn.exe child process, parsing its output to drive
/// the GUI state machine. Passwords are delivered exclusively via stdin pipe.
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

    // --- Output parsing patterns ---
    private static readonly Regex IpAssignedPattern =
        new(@"Remote IP:\s+([\d.]+)", RegexOptions.Compiled);
    private static readonly Regex GatewayIpPattern =
        new(@"Gateway IP:\s+([\d.]+)", RegexOptions.Compiled);
    private static readonly Regex DnsPattern =
        new(@"DNS:\s+([\d.]+)", RegexOptions.Compiled);
    private static readonly Regex InterfacePattern =
        new(@"Interface\s+(\S+)\s+is up", RegexOptions.Compiled);

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
        var appSettings = _settings.Current;

        var args = profile.BuildArguments();

        // Add verbosity flags from settings
        args.AddRange(appSettings.LogVerbosity.ToCliFlags());

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

            // Read output streams on background threads
            _ = Task.Run(() => ReadStreamAsync(_process.StandardOutput, "stdout"), _cts.Token);
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
                ProcessOutputLine(line, source);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Stream {Source} read ended", source);
        }
    }

    private void ProcessOutputLine(string line, string source)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        // Determine severity
        var severity = ClassifySeverity(line);

        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Severity = severity,
            Message = line.TrimStart(),
            RawLine = line,
            Source = source
        };

        LogReceived?.Invoke(this, entry);

        // State machine transitions based on output patterns
        ParseStateTransition(line);
        ParseConnectionDetails(line);
    }

    private void ParseStateTransition(string line)
    {
        if (line.Contains("Resolving gateway", StringComparison.OrdinalIgnoreCase))
        {
            TransitionTo(ConnectionState.Connecting);
        }
        else if (line.Contains("Connected to gateway", StringComparison.OrdinalIgnoreCase))
        {
            TransitionTo(ConnectionState.Authenticating);
        }
        else if (line.Contains("Two-factor authentication", StringComparison.OrdinalIgnoreCase)
                 || line.Contains("OTP", StringComparison.OrdinalIgnoreCase)
                 || line.Contains("token", StringComparison.OrdinalIgnoreCase))
        {
            TransitionTo(ConnectionState.WaitingForOtp);
            OtpRequired?.Invoke(this, EventArgs.Empty);
        }
        else if (line.Contains("SAML", StringComparison.OrdinalIgnoreCase)
                 && line.Contains("http", StringComparison.OrdinalIgnoreCase))
        {
            TransitionTo(ConnectionState.WaitingForSaml);
            // Extract URL for SAML login
            var urlMatch = Regex.Match(line, @"(https?://\S+)");
            if (urlMatch.Success)
            {
                SamlLoginRequired?.Invoke(this, urlMatch.Groups[1].Value);
            }
        }
        else if (line.Contains("Tunnel is up", StringComparison.OrdinalIgnoreCase)
                 || line.Contains("Interface", StringComparison.OrdinalIgnoreCase)
                    && line.Contains("is up", StringComparison.OrdinalIgnoreCase))
        {
            CurrentConnection.ConnectedSince = DateTime.Now;
            TransitionTo(ConnectionState.Connected);
        }
        else if (line.Contains("Tunnel went down", StringComparison.OrdinalIgnoreCase))
        {
            TransitionTo(ConnectionState.Reconnecting);
            CurrentConnection.ReconnectAttempts++;
        }
        else if (line.Contains("authentication failed", StringComparison.OrdinalIgnoreCase)
                 || line.Contains("permission denied", StringComparison.OrdinalIgnoreCase))
        {
            var (category, message) = ErrorClassifier.Classify(line);
            CurrentConnection.ErrorMessage = message;
            CurrentConnection.ErrorCategory = category;
            TransitionTo(ConnectionState.Error);
        }
    }

    private void ParseConnectionDetails(string line)
    {
        var ipMatch = IpAssignedPattern.Match(line);
        if (ipMatch.Success) CurrentConnection.AssignedIp = ipMatch.Groups[1].Value;

        var gwMatch = GatewayIpPattern.Match(line);
        if (gwMatch.Success) CurrentConnection.GatewayIp = gwMatch.Groups[1].Value;

        var dnsMatch = DnsPattern.Match(line);
        if (dnsMatch.Success)
        {
            if (CurrentConnection.Dns1 is null)
                CurrentConnection.Dns1 = dnsMatch.Groups[1].Value;
            else
                CurrentConnection.Dns2 = dnsMatch.Groups[1].Value;
        }

        var ifMatch = InterfacePattern.Match(line);
        if (ifMatch.Success) CurrentConnection.TunnelInterface = ifMatch.Groups[1].Value;
    }

    private static LogSeverity ClassifySeverity(string line)
    {
        if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase)) return LogSeverity.Error;
        if (line.Contains("WARN", StringComparison.OrdinalIgnoreCase)) return LogSeverity.Warning;
        if (line.Contains("DEBUG", StringComparison.OrdinalIgnoreCase)) return LogSeverity.Debug;
        return LogSeverity.Info;
    }

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

        if (State == ConnectionState.Disconnecting)
        {
            TransitionTo(ConnectionState.Disconnected);
        }
        else if (exitCode != 0 && State != ConnectionState.Error)
        {
            CurrentConnection.ErrorMessage = $"VPN process exited unexpectedly (code {exitCode}).";
            CurrentConnection.ErrorCategory = Models.ErrorCategory.ProcessCrashed;
            TransitionTo(ConnectionState.Error);
        }
        else if (State != ConnectionState.Error)
        {
            TransitionTo(ConnectionState.Disconnected);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); } catch { }
        }

        _process?.Dispose();
    }
}
