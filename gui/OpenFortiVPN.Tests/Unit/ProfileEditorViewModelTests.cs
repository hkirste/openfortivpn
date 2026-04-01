using CommunityToolkit.Mvvm.ComponentModel;
using FluentAssertions;
using OpenFortiVPN.GUI.Models;
using OpenFortiVPN.GUI.Services;
using OpenFortiVPN.GUI.ViewModels;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class ProfileEditorViewModelTests
{
    private readonly MockProfileService _profileService = new();
    private readonly MockCredentialService _credentialService = new();
    private readonly MockNavigationService _navigation = new();

    private ProfileEditorViewModel CreateVm() => new(
        _profileService, _credentialService, _navigation);

    [Fact]
    public void InitialState_IsNewProfile()
    {
        var vm = CreateVm();
        vm.PageTitle.Should().Be("New Profile");
        vm.GatewayPort.Should().Be(443);
        vm.SetRoutes.Should().BeTrue();
        vm.SetDns.Should().BeTrue();
        vm.AuthMethod.Should().Be(AuthMethod.UsernamePassword);
        vm.MinTlsVersion.Should().Be(TlsVersion.Default);
    }

    [Fact]
    public void ReceiveParameter_LoadsExistingProfile()
    {
        var vm = CreateVm();
        var profile = new VpnProfile
        {
            Name = "Office VPN",
            GatewayHost = "vpn.corp.com",
            GatewayPort = 8443,
            Username = "john",
            Realm = "CORP",
            SetRoutes = false,
            InsecureSsl = true
        };

        vm.ReceiveParameter(profile);

        vm.PageTitle.Should().Be("Edit Profile");
        vm.ProfileName.Should().Be("Office VPN");
        vm.GatewayHost.Should().Be("vpn.corp.com");
        vm.GatewayPort.Should().Be(8443);
        vm.Username.Should().Be("john");
        vm.Realm.Should().Be("CORP");
        vm.SetRoutes.Should().BeFalse();
        vm.InsecureSsl.Should().BeTrue();
    }

    [Fact]
    public async Task Save_EmptyHost_SetsValidationError()
    {
        var vm = CreateVm();
        vm.GatewayHost = "";
        vm.Username = "user";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ValidationError.Should().NotBeNullOrEmpty();
        vm.ValidationError.Should().Contain("address");
    }

    [Fact]
    public async Task Save_EmptyUsername_SetsValidationError()
    {
        var vm = CreateVm();
        vm.GatewayHost = "vpn.example.com";
        vm.Username = "";
        vm.AuthMethod = AuthMethod.UsernamePassword;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ValidationError.Should().Contain("Username");
    }

    [Fact]
    public async Task Save_ValidProfile_NavigatesBack()
    {
        var vm = CreateVm();
        vm.GatewayHost = "vpn.example.com";
        vm.Username = "john";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ValidationError.Should().BeNull();
        _navigation.WentBack.Should().BeTrue();
        _profileService.SavedProfile.Should().NotBeNull();
        _profileService.SavedProfile!.GatewayHost.Should().Be("vpn.example.com");
    }

    [Fact]
    public async Task Save_WithPassword_SavesCredential()
    {
        var vm = CreateVm();
        vm.GatewayHost = "vpn.example.com";
        vm.Username = "john";
        vm.Password = "secret";
        vm.SavePassword = true;

        await vm.SaveCommand.ExecuteAsync(null);

        _credentialService.SavedPasswords.Should().ContainValue("secret");
    }

    [Fact]
    public async Task Save_NoSavePassword_DeletesCredential()
    {
        var vm = CreateVm();
        vm.GatewayHost = "vpn.example.com";
        vm.Username = "john";
        vm.Password = "secret";
        vm.SavePassword = false;

        await vm.SaveCommand.ExecuteAsync(null);

        _credentialService.DeletedIds.Should().HaveCount(1);
    }

    [Fact]
    public void Cancel_NavigatesBack()
    {
        var vm = CreateVm();
        vm.CancelCommand.Execute(null);

        _navigation.WentBack.Should().BeTrue();
    }

    [Fact]
    public void ReceiveParameter_WithSavedPassword_LoadsIt()
    {
        var profile = new VpnProfile { Name = "Test" };
        _credentialService.SavedPasswords[profile.Id] = "stored-pass";

        var vm = CreateVm();
        vm.ReceiveParameter(profile);

        vm.Password.Should().Be("stored-pass");
        vm.SavePassword.Should().BeTrue();
    }

    [Fact]
    public async Task Save_Persistent_SetsInterval()
    {
        var vm = CreateVm();
        vm.GatewayHost = "vpn.example.com";
        vm.Username = "john";
        vm.EnablePersistent = true;
        vm.PersistentInterval = 30;

        await vm.SaveCommand.ExecuteAsync(null);

        _profileService.SavedProfile!.PersistentInterval.Should().Be(30);
    }

    [Fact]
    public async Task Save_NoPersistent_ZeroInterval()
    {
        var vm = CreateVm();
        vm.GatewayHost = "vpn.example.com";
        vm.Username = "john";
        vm.EnablePersistent = false;
        vm.PersistentInterval = 30;

        await vm.SaveCommand.ExecuteAsync(null);

        _profileService.SavedProfile!.PersistentInterval.Should().Be(0);
    }

    // --- Mocks ---

    #pragma warning disable CS0067
    private sealed class MockProfileService : IProfileService
    {
        public VpnProfile? SavedProfile { get; private set; }
        public IReadOnlyList<VpnProfile> Profiles => new List<VpnProfile>();
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;

        public Task SaveProfileAsync(VpnProfile p)
        {
            SavedProfile = p;
            return Task.CompletedTask;
        }

        public Task DeleteProfileAsync(Guid id) => Task.CompletedTask;
        public VpnProfile ImportFromConfigFile(string path) => new();
        public void ExportToConfigFile(VpnProfile p, string path) { }
        public VpnProfile DuplicateProfile(VpnProfile s) => new();
    }

    #pragma warning disable CS0067
    private sealed class MockCredentialService : ICredentialService
    {
        public Dictionary<Guid, string> SavedPasswords { get; } = new();
        public List<Guid> DeletedIds { get; } = new();

        public void SavePassword(Guid id, string pw) => SavedPasswords[id] = pw;

        public string? LoadPassword(Guid id) =>
            SavedPasswords.TryGetValue(id, out var pw) ? pw : null;

        public void DeletePassword(Guid id) => DeletedIds.Add(id);
        public bool HasPassword(Guid id) => SavedPasswords.ContainsKey(id);
    }

    #pragma warning disable CS0067
    private sealed class MockNavigationService : INavigationService
    {
        public bool WentBack { get; private set; }
        public ObservableObject CurrentViewModel { get; set; } = null!;
        public event EventHandler<ObservableObject>? Navigated;
        public bool CanGoBack => false;

        public void NavigateTo<T>()
            where T : ObservableObject { }

        public void NavigateTo<T>(object p)
            where T : ObservableObject { }

        public void GoBack() => WentBack = true;
    }
}
#pragma warning restore CS0067
