using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFortiVPN.GUI.Services;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class CredentialServiceTests : IDisposable
{
    private readonly CredentialService _service;
    private readonly Guid _testId = Guid.NewGuid();

    public CredentialServiceTests()
    {
        _service = new CredentialService(new NullLogger<CredentialService>());
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_ReturnsOriginalPassword()
    {
        _service.SavePassword(_testId, "s3cret!");
        var loaded = _service.LoadPassword(_testId);
        loaded.Should().Be("s3cret!");
    }

    [Fact]
    public void HasPassword_AfterSave_ReturnsTrue()
    {
        _service.SavePassword(_testId, "pass");
        _service.HasPassword(_testId).Should().BeTrue();
    }

    [Fact]
    public void HasPassword_BeforeSave_ReturnsFalse()
    {
        _service.HasPassword(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void LoadPassword_NonExistent_ReturnsNull()
    {
        _service.LoadPassword(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void DeletePassword_RemovesCredential()
    {
        _service.SavePassword(_testId, "pass");
        _service.HasPassword(_testId).Should().BeTrue();

        _service.DeletePassword(_testId);
        _service.HasPassword(_testId).Should().BeFalse();
        _service.LoadPassword(_testId).Should().BeNull();
    }

    [Fact]
    public void DeletePassword_NonExistent_DoesNotThrow()
    {
        var act = () => _service.DeletePassword(Guid.NewGuid());
        act.Should().NotThrow();
    }

    [Fact]
    public void SavePassword_Overwrite_ReturnsNewValue()
    {
        _service.SavePassword(_testId, "old");
        _service.SavePassword(_testId, "new");
        _service.LoadPassword(_testId).Should().Be("new");
    }

    [Fact]
    public void SavePassword_UnicodeCharacters_RoundTrips()
    {
        _service.SavePassword(_testId, "p@ss\u00e9\u00fc\u00f1");
        _service.LoadPassword(_testId).Should().Be("p@ss\u00e9\u00fc\u00f1");
    }

    [Fact]
    public void SavePassword_EmptyString_RoundTrips()
    {
        _service.SavePassword(_testId, "");
        _service.LoadPassword(_testId).Should().Be("");
    }

    public void Dispose()
    {
        _service.DeletePassword(_testId);
    }
}
