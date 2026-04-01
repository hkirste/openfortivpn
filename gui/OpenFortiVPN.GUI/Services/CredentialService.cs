using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OpenFortiVPN.GUI.Services;

/// <summary>
/// Stores VPN passwords encrypted with DPAPI (Windows Data Protection API).
/// Each password is stored as a separate file in the user's AppData directory.
///
/// Security properties:
/// - Encrypted with the Windows user's account key (CurrentUser scope)
/// - Cannot be decrypted by other users or on other machines
/// - Additional entropy provides defense-in-depth
/// - Files are only readable by the current user (NTFS ACLs)
/// </summary>
public sealed class CredentialService : ICredentialService
{
    private readonly ILogger<CredentialService> _logger;
    private readonly string _credentialDir;

    // Additional entropy for DPAPI — not a secret, but adds a layer
    private static readonly byte[] Entropy =
        "OpenFortiVPN.GUI.CredentialEntropy.v1"u8.ToArray();

    public CredentialService(ILogger<CredentialService> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _credentialDir = Path.Combine(appData, "OpenFortiVPN", "credentials");
        Directory.CreateDirectory(_credentialDir);
    }

    public void SavePassword(Guid profileId, string password)
    {
        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(password);
            var encrypted = ProtectedData.Protect(plainBytes, Entropy,
                DataProtectionScope.CurrentUser);

            var filePath = GetCredentialPath(profileId);
            File.WriteAllBytes(filePath, encrypted);

            // Zero out the plaintext bytes immediately
            Array.Clear(plainBytes, 0, plainBytes.Length);

            _logger.LogDebug("Saved credential for profile {ProfileId}", profileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save credential for profile {ProfileId}", profileId);
            throw;
        }
    }

    public string? LoadPassword(Guid profileId)
    {
        var filePath = GetCredentialPath(profileId);
        if (!File.Exists(filePath)) return null;

        try
        {
            var encrypted = File.ReadAllBytes(filePath);
            var plainBytes = ProtectedData.Unprotect(encrypted, Entropy,
                DataProtectionScope.CurrentUser);

            var password = Encoding.UTF8.GetString(plainBytes);

            // Zero out decrypted bytes
            Array.Clear(plainBytes, 0, plainBytes.Length);

            return password;
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex,
                "Cannot decrypt credential for {ProfileId} — may have been created by another user",
                profileId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load credential for profile {ProfileId}", profileId);
            return null;
        }
    }

    public void DeletePassword(Guid profileId)
    {
        var filePath = GetCredentialPath(profileId);
        if (File.Exists(filePath))
        {
            // Overwrite with zeros before deleting for defense-in-depth
            var length = new FileInfo(filePath).Length;
            File.WriteAllBytes(filePath, new byte[length]);
            File.Delete(filePath);
            _logger.LogDebug("Deleted credential for profile {ProfileId}", profileId);
        }
    }

    public bool HasPassword(Guid profileId)
    {
        return File.Exists(GetCredentialPath(profileId));
    }

    private string GetCredentialPath(Guid profileId)
    {
        return Path.Combine(_credentialDir, $"{profileId:N}.cred");
    }
}
