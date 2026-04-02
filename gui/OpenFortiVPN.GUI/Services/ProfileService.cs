using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenFortiVPN.GUI.Models;

namespace OpenFortiVPN.GUI.Services;

public sealed class ProfileService : IProfileService
{
    private readonly ILogger<ProfileService> _logger;
    private readonly string _profilesPath;
    private readonly List<VpnProfile> _profiles = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public IReadOnlyList<VpnProfile> Profiles => _profiles.AsReadOnly();

    public ProfileService(ILogger<ProfileService> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "OpenFortiVPN");
        Directory.CreateDirectory(dir);
        _profilesPath = Path.Combine(dir, "profiles.json");
    }

    public async Task LoadAsync()
    {
        _logger.LogDebug("Looking for profiles at {Path}", _profilesPath);

        if (!File.Exists(_profilesPath))
        {
            _logger.LogInformation("No profiles file found, starting fresh");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_profilesPath);
            _logger.LogDebug("Read {Len} bytes from profiles file", json.Length);
            var profiles = JsonSerializer.Deserialize<List<VpnProfile>>(json, JsonOptions);
            if (profiles is not null)
            {
                _profiles.Clear();
                _profiles.AddRange(profiles);
                _logger.LogInformation("Loaded {Count} profiles", _profiles.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profiles from {Path}", _profilesPath);
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_profiles, JsonOptions);
            await File.WriteAllTextAsync(_profilesPath, json);
            _logger.LogDebug("Saved {Count} profiles", _profiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save profiles");
        }
    }

    public async Task SaveProfileAsync(VpnProfile profile)
    {
        var idx = _profiles.FindIndex(p => p.Id == profile.Id);
        if (idx >= 0)
            _profiles[idx] = profile;
        else
            _profiles.Add(profile);

        await SaveAsync();
    }

    public async Task DeleteProfileAsync(Guid id)
    {
        _profiles.RemoveAll(p => p.Id == id);
        await SaveAsync();
    }

    public VpnProfile ImportFromConfigFile(string path)
    {
        var profile = new VpnProfile { Name = Path.GetFileNameWithoutExtension(path) };

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            var eqIndex = line.IndexOf('=');
            if (eqIndex < 0) continue;

            var key = line[..eqIndex].Trim();
            var value = line[(eqIndex + 1)..].Trim().Trim('"');

            switch (key)
            {
                case "host": profile.GatewayHost = value; break;
                case "port": profile.GatewayPort = int.TryParse(value, out var p) ? p : 443; break;
                case "username": profile.Username = value; break;
                case "realm": profile.Realm = value; break;
                case "sni": profile.SniOverride = value; break;
                case "user-cert": profile.UserCertPath = value; break;
                case "user-key": profile.UserKeyPath = value; break;
                case "ca-file": profile.CaFilePath = value; break;
                case "trusted-cert": profile.TrustedCertDigests.Add(value); break;
                case "set-routes":
                    profile.SetRoutes = value != "0";
                    break;
                case "set-dns":
                    profile.SetDns = value != "0";
                    break;
                case "half-internet-routes":
                    profile.HalfInternetRoutes = value == "1";
                    break;
                case "insecure-ssl":
                    profile.InsecureSsl = value == "1";
                    break;
                case "cipher-list": profile.CipherList = value; break;
                case "min-tls":
                    profile.MinTlsVersion = value switch
                    {
                        "1.0" => TlsVersion.Tls10,
                        "1.1" => TlsVersion.Tls11,
                        "1.2" => TlsVersion.Tls12,
                        "1.3" => TlsVersion.Tls13,
                        _ => TlsVersion.Default
                    };
                    break;
                case "seclevel-1": profile.SecurityLevel1 = value == "1"; break;
                case "persistent":
                    profile.PersistentInterval = int.TryParse(value, out var pi) ? pi : 0;
                    break;
            }
        }

        _logger.LogInformation("Imported profile '{Name}' from {Path}", profile.Name, path);
        return profile;
    }

    public void ExportToConfigFile(VpnProfile profile, string path)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("# OpenFortiVPN configuration");
        writer.WriteLine($"# Exported from GUI: {profile.Name}");
        writer.WriteLine($"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine();

        writer.WriteLine($"host = {profile.GatewayHost}");
        writer.WriteLine($"port = {profile.GatewayPort}");

        if (!string.IsNullOrWhiteSpace(profile.Username))
            writer.WriteLine($"username = {profile.Username}");

        if (!string.IsNullOrWhiteSpace(profile.Realm))
            writer.WriteLine($"realm = {profile.Realm}");

        if (!string.IsNullOrWhiteSpace(profile.SniOverride))
            writer.WriteLine($"sni = {profile.SniOverride}");

        if (!string.IsNullOrWhiteSpace(profile.UserCertPath))
            writer.WriteLine($"user-cert = {profile.UserCertPath}");

        if (!string.IsNullOrWhiteSpace(profile.UserKeyPath))
            writer.WriteLine($"user-key = {profile.UserKeyPath}");

        if (!string.IsNullOrWhiteSpace(profile.CaFilePath))
            writer.WriteLine($"ca-file = {profile.CaFilePath}");

        foreach (var digest in profile.TrustedCertDigests)
            writer.WriteLine($"trusted-cert = {digest}");

        writer.WriteLine($"set-routes = {(profile.SetRoutes ? "1" : "0")}");
        writer.WriteLine($"set-dns = {(profile.SetDns ? "1" : "0")}");

        if (profile.HalfInternetRoutes)
            writer.WriteLine("half-internet-routes = 1");

        if (profile.InsecureSsl)
            writer.WriteLine("insecure-ssl = 1");

        if (!string.IsNullOrWhiteSpace(profile.CipherList))
            writer.WriteLine($"cipher-list = {profile.CipherList}");

        if (profile.MinTlsVersion != TlsVersion.Default)
            writer.WriteLine($"min-tls = {profile.MinTlsVersion.ToCliValue()}");

        if (profile.SecurityLevel1)
            writer.WriteLine("seclevel-1 = 1");

        if (profile.PersistentInterval > 0)
            writer.WriteLine($"persistent = {profile.PersistentInterval}");

        _logger.LogInformation("Exported profile '{Name}' to {Path}", profile.Name, path);
    }

    public VpnProfile DuplicateProfile(VpnProfile source)
    {
        var json = JsonSerializer.Serialize(source, JsonOptions);
        var copy = JsonSerializer.Deserialize<VpnProfile>(json, JsonOptions)!;
        copy.Id = Guid.NewGuid();
        copy.Name = $"{source.Name} (Copy)";
        copy.CreatedAt = DateTime.UtcNow;
        copy.LastConnectedAt = null;
        return copy;
    }
}
