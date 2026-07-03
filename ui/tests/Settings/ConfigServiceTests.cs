using LimelightX.UI.Routing;
using LimelightX.UI.Services;
using Xunit;

namespace LimelightX.UI.Tests.Settings;

public class ConfigServiceTests
{
    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var service = new ConfigService(path);

        Assert.Null(service.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsCorrectly()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var service = new ConfigService(path);
            var config = new AppConfig { Port = 5000, LogPath = @"C:\logs\llx.log", EnvironmentProfile = EnvironmentProfile.Stage };

            service.Save(config);
            var loaded = service.Load();

            Assert.NotNull(loaded);
            Assert.Equal(5000, loaded!.Port);
            Assert.Equal(@"C:\logs\llx.log", loaded.LogPath);
            Assert.Equal(EnvironmentProfile.Stage, loaded.EnvironmentProfile);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Save_UsesCamelCaseWireFormat()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            var service = new ConfigService(path);
            service.Save(new AppConfig { Port = 4747, LogPath = "", EnvironmentProfile = EnvironmentProfile.Dev });

            var json = File.ReadAllText(path);

            // ui-deployment.md §4.3's exact schema: port, logPath, environmentProfile.
            Assert.Contains("\"port\"", json);
            Assert.Contains("\"logPath\"", json);
            Assert.Contains("\"environmentProfile\"", json);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_MalformedJson_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{ not valid json");
            var service = new ConfigService(path);

            Assert.Null(service.Load());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
