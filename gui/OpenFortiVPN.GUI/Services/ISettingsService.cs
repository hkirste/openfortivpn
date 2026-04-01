using OpenFortiVPN.GUI.Models;

namespace OpenFortiVPN.GUI.Services;

/// <summary>
/// Loads, saves, and provides access to application settings.
/// </summary>
public interface ISettingsService
{
    AppSettings Current { get; }
    Task LoadAsync();
    Task SaveAsync();
}
