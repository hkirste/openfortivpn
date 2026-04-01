using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;

namespace OpenFortiVPN.GUI.ViewModels;

/// <summary>
/// ViewModel for creating/editing a VPN profile. Uses progressive disclosure:
/// basic fields are always visible, advanced sections expand on demand.
/// </summary>
public partial class ProfileEditorViewModel : ObservableObject, IParameterReceiver
{
    private readonly IProfileService _profileService;
    private readonly ICredentialService _credentialService;
    private readonly INavigationService _navigation;
    private bool _isNew = true;

    // --- Basic ---

    [ObservableProperty]
    private string _profileName = "New Connection";

    [ObservableProperty]
    private string _gatewayHost = string.Empty;

    [ObservableProperty]
    private int _gatewayPort = 443;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _savePassword = true;

    [ObservableProperty]
    private string? _realm;

    // --- Authentication ---

    [ObservableProperty]
    private AuthMethod _authMethod = AuthMethod.UsernamePassword;

    [ObservableProperty]
    private string? _userCertPath;

    [ObservableProperty]
    private string? _userKeyPath;

    [ObservableProperty]
    private string? _caFilePath;

    [ObservableProperty]
    private string _trustedCertsText = string.Empty;

    [ObservableProperty]
    private bool _useSaml;

    [ObservableProperty]
    private int _samlPort = 8020;

    // --- Network ---

    [ObservableProperty]
    private bool _setRoutes = true;

    [ObservableProperty]
    private bool _setDns = true;

    [ObservableProperty]
    private bool _halfInternetRoutes;

    // --- TLS/Security ---

    [ObservableProperty]
    private bool _insecureSsl;

    [ObservableProperty]
    private string? _cipherList;

    [ObservableProperty]
    private TlsVersion _minTlsVersion = TlsVersion.Default;

    [ObservableProperty]
    private bool _securityLevel1;

    [ObservableProperty]
    private string? _sniOverride;

    // --- Reconnection ---

    [ObservableProperty]
    private bool _enablePersistent;

    [ObservableProperty]
    private int _persistentInterval = 10;

    // --- UI State ---

    [ObservableProperty]
    private bool _showAuthSection;

    [ObservableProperty]
    private bool _showNetworkSection;

    [ObservableProperty]
    private bool _showSecuritySection;

    [ObservableProperty]
    private bool _showAdvancedSection;

    [ObservableProperty]
    private string _pageTitle = "New Profile";

    [ObservableProperty]
    private string? _validationError;

    // Available enum values for combo boxes
    public AuthMethod[] AuthMethods { get; } = Enum.GetValues<AuthMethod>();
    public TlsVersion[] TlsVersions { get; } = Enum.GetValues<TlsVersion>();

    private Guid _profileId = Guid.NewGuid();

    public ProfileEditorViewModel(
        IProfileService profileService,
        ICredentialService credentialService,
        INavigationService navigation)
    {
        _profileService = profileService;
        _credentialService = credentialService;
        _navigation = navigation;
    }

    /// <summary>
    /// Called when navigating with a profile to edit.
    /// </summary>
    public void ReceiveParameter(object parameter)
    {
        if (parameter is VpnProfile profile)
        {
            _isNew = false;
            _profileId = profile.Id;
            PageTitle = "Edit Profile";

            ProfileName = profile.Name;
            GatewayHost = profile.GatewayHost;
            GatewayPort = profile.GatewayPort;
            Username = profile.Username;
            Realm = profile.Realm;
            SniOverride = profile.SniOverride;

            AuthMethod = profile.AuthMethod;
            UserCertPath = profile.UserCertPath;
            UserKeyPath = profile.UserKeyPath;
            CaFilePath = profile.CaFilePath;
            TrustedCertsText = string.Join("\n", profile.TrustedCertDigests);
            UseSaml = profile.UseSaml;
            SamlPort = profile.SamlPort;

            SetRoutes = profile.SetRoutes;
            SetDns = profile.SetDns;
            HalfInternetRoutes = profile.HalfInternetRoutes;

            InsecureSsl = profile.InsecureSsl;
            CipherList = profile.CipherList;
            MinTlsVersion = profile.MinTlsVersion;
            SecurityLevel1 = profile.SecurityLevel1;

            EnablePersistent = profile.PersistentInterval > 0;
            PersistentInterval = profile.PersistentInterval > 0 ? profile.PersistentInterval : 10;

            // Load saved password
            if (_credentialService.HasPassword(profile.Id))
            {
                Password = _credentialService.LoadPassword(profile.Id) ?? string.Empty;
                SavePassword = true;
            }
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        // Validation
        if (string.IsNullOrWhiteSpace(GatewayHost))
        {
            ValidationError = "Server address is required.";
            return;
        }

        if (AuthMethod == AuthMethod.UsernamePassword && string.IsNullOrWhiteSpace(Username))
        {
            ValidationError = "Username is required for password authentication.";
            return;
        }

        ValidationError = null;

        var profile = new VpnProfile
        {
            Id = _profileId,
            Name = string.IsNullOrWhiteSpace(ProfileName) ? GatewayHost : ProfileName,
            CreatedAt = _isNew ? DateTime.UtcNow : DateTime.UtcNow,
            GatewayHost = GatewayHost.Trim(),
            GatewayPort = GatewayPort,
            Username = Username.Trim(),
            Realm = string.IsNullOrWhiteSpace(Realm) ? null : Realm.Trim(),
            SniOverride = string.IsNullOrWhiteSpace(SniOverride) ? null : SniOverride.Trim(),

            AuthMethod = AuthMethod,
            UserCertPath = UserCertPath,
            UserKeyPath = UserKeyPath,
            CaFilePath = CaFilePath,
            TrustedCertDigests = TrustedCertsText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList(),
            UseSaml = UseSaml,
            SamlPort = SamlPort,

            SetRoutes = SetRoutes,
            SetDns = SetDns,
            HalfInternetRoutes = HalfInternetRoutes,

            InsecureSsl = InsecureSsl,
            CipherList = string.IsNullOrWhiteSpace(CipherList) ? null : CipherList,
            MinTlsVersion = MinTlsVersion,
            SecurityLevel1 = SecurityLevel1,

            PersistentInterval = EnablePersistent ? PersistentInterval : 0,
        };

        // Save credential securely
        if (SavePassword && !string.IsNullOrEmpty(Password))
        {
            _credentialService.SavePassword(profile.Id, Password);
        }
        else
        {
            _credentialService.DeletePassword(profile.Id);
        }

        await _profileService.SaveProfileAsync(profile);
        _navigation.GoBack();
    }

    [RelayCommand]
    private void Cancel()
    {
        _navigation.GoBack();
    }

    [RelayCommand]
    private void BrowseUserCert()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Client Certificate",
            Filter = "PEM Certificates (*.pem;*.crt;*.cer)|*.pem;*.crt;*.cer|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            UserCertPath = dlg.FileName;
    }

    [RelayCommand]
    private void BrowseUserKey()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Client Private Key",
            Filter = "PEM Keys (*.pem;*.key)|*.pem;*.key|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            UserKeyPath = dlg.FileName;
    }

    [RelayCommand]
    private void BrowseCaFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select CA Certificate Bundle",
            Filter = "PEM Certificates (*.pem;*.crt;*.cer)|*.pem;*.crt;*.cer|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            CaFilePath = dlg.FileName;
    }

    [RelayCommand]
    private void ToggleAuthSection() => ShowAuthSection = !ShowAuthSection;

    [RelayCommand]
    private void ToggleNetworkSection() => ShowNetworkSection = !ShowNetworkSection;

    [RelayCommand]
    private void ToggleSecuritySection() => ShowSecuritySection = !ShowSecuritySection;

    [RelayCommand]
    private void ToggleAdvancedSection() => ShowAdvancedSection = !ShowAdvancedSection;
}
