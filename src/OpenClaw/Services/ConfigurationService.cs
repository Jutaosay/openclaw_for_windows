// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Text.Json;
using OpenClaw.Models;

namespace OpenClaw.Services;

/// <summary>
/// Manages application settings persistence using JSON file storage.
/// Settings are stored in %LOCALAPPDATA%\OpenClaw\settings.json.
/// </summary>
public class ConfigurationService
{
    private static readonly string AppDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenClaw");

    private static readonly string SettingsFilePath =
        Path.Combine(AppDataFolder, "settings.json");

    private readonly object _lock = new();

    /// <summary>
    /// Gets the current application settings.
    /// </summary>
    public AppSettings Settings { get; private set; } = new();

    /// <summary>
    /// Loads settings from disk. Creates defaults if the file doesn't exist.
    /// </summary>
    public void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
                    if (settings is not null)
                    {
                        Settings = settings;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to load settings: {ex.Message}");
            }

            // Create default settings with a sample environment
            Settings = new AppSettings
            {
                Environments =
                [
                    new EnvironmentConfig
                    {
                        Name = "Default",
                        GatewayUrl = "https://example.com",
                        IsDefault = true,
                    }
                ],
                SelectedEnvironmentName = "Default",
            };
            Save();
        }
    }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    public void Save()
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(AppDataFolder);
                var json = JsonSerializer.Serialize(Settings, AppSettingsJsonContext.Default.AppSettings);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to save settings: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets the default environment, or the first available, or null.
    /// </summary>
    public EnvironmentConfig? GetDefaultEnvironment()
    {
        return Settings.Environments.FirstOrDefault(e => e.IsDefault)
            ?? Settings.Environments.FirstOrDefault();
    }

    /// <summary>
    /// Gets the currently selected environment by persisted name.
    /// Falls back to default if the named environment is not found.
    /// </summary>
    public EnvironmentConfig? GetSelectedEnvironment()
    {
        if (!string.IsNullOrEmpty(Settings.SelectedEnvironmentName))
        {
            var env = Settings.Environments.FirstOrDefault(
                e => e.Name.Equals(Settings.SelectedEnvironmentName, StringComparison.Ordinal));
            if (env is not null) return env;
        }

        return GetDefaultEnvironment();
    }
}
