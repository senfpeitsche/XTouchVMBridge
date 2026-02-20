using Microsoft.Extensions.Logging.Abstractions;
using XTouchVMBridge.Voicemeeter.Services;

namespace XTouchVMBridge.Tests.Configuration;

public class ConfigurationServiceMigrationTests
{
    [Fact]
    public void Load_ConfigWithoutVersion_MigratesAndPersistsCurrentVersion()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");
        try
        {
            File.WriteAllText(configPath, """
            {
              "voicemeeterApiType": "potato",
              "channels": {
                "0": { "name": "Mic", "type": "Hardware Input 1", "color": "green" }
              }
            }
            """);

            var service = new ConfigurationService(NullLogger<ConfigurationService>.Instance, configPath);

            var config = service.Load();
            var savedConfig = File.ReadAllText(configPath);

            Assert.Equal(ConfigurationService.CurrentConfigVersion, config.ConfigVersion);
            Assert.Contains("\"configVersion\": 1", savedConfig);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Load_FutureConfigVersion_IsKept()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");
        try
        {
            File.WriteAllText(configPath, """
            {
              "configVersion": 99,
              "channels": {}
            }
            """);

            var service = new ConfigurationService(NullLogger<ConfigurationService>.Instance, configPath);

            var config = service.Load();

            Assert.Equal(99, config.ConfigVersion);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
