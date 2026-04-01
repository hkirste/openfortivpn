using OpenFortiVPN.GUI.Models;

namespace OpenFortiVPN.GUI.Services;

/// <summary>
/// Manages VPN connection profiles — load, save, delete, import, export.
/// Profiles are stored as JSON in the user's AppData directory.
/// </summary>
public interface IProfileService
{
    /// <summary>All saved profiles.</summary>
    IReadOnlyList<VpnProfile> Profiles { get; }

    /// <summary>Load profiles from disk.</summary>
    Task LoadAsync();

    /// <summary>Save all profiles to disk.</summary>
    Task SaveAsync();

    /// <summary>Add or update a profile.</summary>
    Task SaveProfileAsync(VpnProfile profile);

    /// <summary>Remove a profile by ID.</summary>
    Task DeleteProfileAsync(Guid id);

    /// <summary>Import a profile from an openfortivpn config file.</summary>
    VpnProfile ImportFromConfigFile(string path);

    /// <summary>Export a profile to openfortivpn config file format.</summary>
    void ExportToConfigFile(VpnProfile profile, string path);

    /// <summary>Create a deep copy of a profile with a new ID.</summary>
    VpnProfile DuplicateProfile(VpnProfile source);
}
