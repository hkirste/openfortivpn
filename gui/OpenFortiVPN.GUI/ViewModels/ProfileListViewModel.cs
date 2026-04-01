using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;

namespace OpenFortiVPN.GUI.ViewModels;

/// <summary>
/// ViewModel for the profile list/management view.
/// </summary>
public partial class ProfileListViewModel : ObservableObject
{
    private readonly IProfileService _profileService;
    private readonly ICredentialService _credentialService;
    private readonly INavigationService _navigation;

    public ObservableCollection<VpnProfile> Profiles { get; } = new();

    [ObservableProperty]
    private VpnProfile? _selectedProfile;

    [ObservableProperty]
    private bool _hasProfiles;

    public ProfileListViewModel(
        IProfileService profileService,
        ICredentialService credentialService,
        INavigationService navigation)
    {
        _profileService = profileService;
        _credentialService = credentialService;
        _navigation = navigation;

        RefreshProfiles();
    }

    private void RefreshProfiles()
    {
        Profiles.Clear();
        foreach (var p in _profileService.Profiles)
            Profiles.Add(p);
        HasProfiles = Profiles.Count > 0;
    }

    [RelayCommand]
    private void CreateNew()
    {
        _navigation.NavigateTo<ProfileEditorViewModel>();
    }

    [RelayCommand]
    private void EditProfile(VpnProfile? profile)
    {
        if (profile is not null)
            _navigation.NavigateTo<ProfileEditorViewModel>(profile);
    }

    [RelayCommand]
    private async Task DeleteProfile(VpnProfile? profile)
    {
        if (profile is null) return;

        _credentialService.DeletePassword(profile.Id);
        await _profileService.DeleteProfileAsync(profile.Id);
        RefreshProfiles();
    }

    [RelayCommand]
    private async Task DuplicateProfile(VpnProfile? profile)
    {
        if (profile is null) return;

        var copy = _profileService.DuplicateProfile(profile);
        await _profileService.SaveProfileAsync(copy);
        RefreshProfiles();
    }

    [RelayCommand]
    private void ImportProfile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import OpenFortiVPN Configuration",
            Filter = "Config Files (*.conf;*.cfg)|*.conf;*.cfg|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            var profile = _profileService.ImportFromConfigFile(dlg.FileName);
            _ = _profileService.SaveProfileAsync(profile);
            RefreshProfiles();
        }
    }

    [RelayCommand]
    private void ExportProfile(VpnProfile? profile)
    {
        if (profile is null) return;

        var dlg = new SaveFileDialog
        {
            Title = "Export OpenFortiVPN Configuration",
            FileName = $"{profile.Name}.conf",
            Filter = "Config Files (*.conf)|*.conf|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            _profileService.ExportToConfigFile(profile, dlg.FileName);
        }
    }
}
