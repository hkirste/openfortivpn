using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenFortiVPN.GUI.Models;

namespace OpenFortiVPN.GUI.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AppSettings Current { get; private set; } = new();

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "OpenFortiVPN");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            _logger.LogInformation("No settings file found, using defaults");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is not null)
            {
                Current = settings;
                _logger.LogInformation("Loaded settings from {Path}", _settingsPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings, using defaults");
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json);
            _logger.LogDebug("Settings saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }
}
