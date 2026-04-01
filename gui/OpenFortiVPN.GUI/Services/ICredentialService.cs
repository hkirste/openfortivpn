namespace OpenFortiVPN.GUI.Services;

/// <summary>
/// Securely stores and retrieves credentials using Windows DPAPI.
/// Passwords are encrypted with the current user's Windows account key,
/// meaning they cannot be decrypted by other users or on other machines.
/// </summary>
public interface ICredentialService
{
    /// <summary>Encrypt and store a password for a profile.</summary>
    void SavePassword(Guid profileId, string password);

    /// <summary>Retrieve and decrypt a stored password.</summary>
    string? LoadPassword(Guid profileId);

    /// <summary>Remove a stored password.</summary>
    void DeletePassword(Guid profileId);

    /// <summary>Check if a password is stored for a profile.</summary>
    bool HasPassword(Guid profileId);
}
