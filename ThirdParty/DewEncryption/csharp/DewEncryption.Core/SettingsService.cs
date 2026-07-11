using System.Text.Json;
using System.Text.Json.Serialization;

namespace DewEncryption.Core;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ConfigFilePath { get; }

    public SettingsService(string? configFilePath = null)
    {
        ConfigFilePath = configFilePath ?? ResolveConfigFilePath();
    }

    public AppSettings Load()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return AppSettings.Default;
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigFilePath), JsonOptions) ?? AppSettings.Default;
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
        catch (IOException)
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static string ResolveConfigFilePath()
    {
        string? explicitConfig = Environment.GetEnvironmentVariable("DEW_ENCRYPTION_CONFIG");
        if (!string.IsNullOrWhiteSpace(explicitConfig))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(explicitConfig));
        }

        string appBase = AppContext.BaseDirectory;
        if (Environment.GetEnvironmentVariable("DEW_ENCRYPTION_PORTABLE") == "1" || File.Exists(Path.Combine(appBase, "portable.flag")))
        {
            return Path.Combine(appBase, "settings.json");
        }

        string baseConfig = Environment.GetEnvironmentVariable("APPDATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(baseConfig, "DewEncryption", "settings.json");
    }
}
