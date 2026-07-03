using System.Text.Json;
using System.Text.Json.Serialization;
using LimelightX.UI.Routing;

namespace LimelightX.UI.Services;

/// <summary>
/// Concrete IConfigService. Single responsibility: serialize/deserialize
/// AppConfig to/from the JSON file - no validation logic beyond shape
/// (field-level validation lives in SettingsViewModel per
/// ui-error-handling.md §10).
/// </summary>
public sealed class ConfigService(string? configFilePathOverride = null) : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ConfigFilePath { get; } = configFilePathOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LimelightX",
        "config.json");

    public AppConfig? Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(ConfigFilePath);
            var dto = JsonSerializer.Deserialize<ConfigFileDto>(json, JsonOptions);
            if (dto is null)
            {
                return null;
            }

            return new AppConfig
            {
                Port = dto.Port,
                LogPath = dto.LogPath ?? string.Empty,
                EnvironmentProfile = Enum.TryParse<EnvironmentProfile>(dto.EnvironmentProfile, ignoreCase: true, out var profile)
                    ? profile
                    : EnvironmentProfile.Dev,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var dto = new ConfigFileDto
        {
            Port = config.Port,
            LogPath = config.LogPath,
            EnvironmentProfile = config.EnvironmentProfile.ToString(),
        };

        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(dto, JsonOptions));
    }

    private sealed class ConfigFileDto
    {
        public int Port { get; init; }

        public string? LogPath { get; init; }

        public string? EnvironmentProfile { get; init; }
    }
}
