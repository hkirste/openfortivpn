using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFortiVPN.GUI.Services;
using Xunit;

namespace OpenFortiVPN.Tests.Unit;

public class ProfileServiceImportTests
{
    private readonly ProfileService _service;

    public ProfileServiceImportTests()
    {
        var logger = NullLoggerFactory.Instance.CreateLogger<ProfileService>();
        _service = new ProfileService(logger);
    }

    private static string GetTestDataPath(string filename)
    {
        // Walk up from the output directory to find the TestData folder in the project
        var dir = AppContext.BaseDirectory;
        var testDataPath = Path.Combine(dir, "TestData", filename);
        if (File.Exists(testDataPath))
            return testDataPath;

        // Fallback: search relative to the project directory
        var current = new DirectoryInfo(dir);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "TestData", filename);
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException($"Test data file not found: {filename}");
    }

    [Fact]
    public void ImportFromConfigFile_BasicConfig_ParsesHostPortUsername()
    {
        var path = GetTestDataPath("sample-config.txt");

        var profile = _service.ImportFromConfigFile(path);

        profile.GatewayHost.Should().Be("vpn.example.com");
        profile.GatewayPort.Should().Be(10443);
        profile.Username.Should().Be("john.doe");
        profile.Realm.Should().Be("CORP");
        profile.Name.Should().Be("sample-config");
    }

    [Fact]
    public void ImportFromConfigFile_TrustedCerts()
    {
        var path = GetTestDataPath("sample-config.txt");

        var profile = _service.ImportFromConfigFile(path);

        profile.TrustedCertDigests.Should().ContainSingle()
            .Which.Should().Be("aabbccdd11223344");
    }

    [Fact]
    public void ImportFromConfigFile_BooleanSettings()
    {
        var path = GetTestDataPath("sample-config.txt");

        var profile = _service.ImportFromConfigFile(path);

        profile.SetRoutes.Should().BeTrue();
        profile.SetDns.Should().BeTrue();
    }

    [Fact]
    public void ImportFromConfigFile_CommentsAndBlankLinesSkipped()
    {
        // Create a temporary config file with comments and blank lines
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-config-{Guid.NewGuid()}.txt");
        try
        {
            File.WriteAllText(tempPath, """
                # This is a comment
                host = vpn.test.org

                # Another comment
                port = 8443
                username = testuser

                """);

            var profile = _service.ImportFromConfigFile(tempPath);

            profile.GatewayHost.Should().Be("vpn.test.org");
            profile.GatewayPort.Should().Be(8443);
            profile.Username.Should().Be("testuser");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
