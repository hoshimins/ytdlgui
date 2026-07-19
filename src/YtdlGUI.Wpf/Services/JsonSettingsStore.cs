using System.Text.Json;
using YtdlGUI.Wpf.Models;

namespace YtdlGUI.Wpf.Services;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly string _legacyConfigPath;

    public JsonSettingsStore(string? settingsPath = null, string? legacyConfigPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YtdlGUI",
            "settings.json");
        _legacyConfigPath = legacyConfigPath ?? Path.Combine(AppContext.BaseDirectory, "config.ini");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_settingsPath))
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
            if (settings is not null)
            {
                return Normalize(settings);
            }
        }

        var migrated = TryReadLegacyOutputDirectory();
        var defaults = new AppSettings(migrated ?? GetDefaultOutputDirectory());
        await SaveAsync(defaults, cancellationToken);
        return defaults;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("設定ファイルの保存先を解決できません。");
        Directory.CreateDirectory(directory);

        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, Normalize(settings), JsonOptions, cancellationToken);
    }

    private string? TryReadLegacyOutputDirectory()
    {
        if (!File.Exists(_legacyConfigPath))
        {
            return null;
        }

        var inSettings = false;
        foreach (var rawLine in File.ReadLines(_legacyConfigPath))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inSettings = string.Equals(line, "[Settings]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSettings)
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (string.Equals(key, "output_directory", StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(value))
            {
                return value;
            }
        }

        return null;
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        var outputDirectory = Directory.Exists(settings.OutputDirectory)
            ? settings.OutputDirectory
            : GetDefaultOutputDirectory();
        return settings with { OutputDirectory = outputDirectory };
    }

    private static string GetDefaultOutputDirectory()
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        return Directory.Exists(downloads)
            ? downloads
            : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    }
}
