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
    private int _saveQueued;
    private int _deferredSaveRequests;
    private int _deferredSaveCoalescedRequests;

    /// <summary>
    /// Gets the current application settings.
    /// </summary>
    public AppSettings Settings { get; private set; } = new();

    public int DeferredSaveRequests => Volatile.Read(ref _deferredSaveRequests);

    public int DeferredSaveCoalescedRequests => Volatile.Read(ref _deferredSaveCoalescedRequests);

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
                        NormalizeSettings(settings, json);
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
            NormalizeSettings(Settings);
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
                NormalizeSettings(Settings);
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

    public void SaveDeferred()
    {
        Interlocked.Increment(ref _deferredSaveRequests);
        if (Interlocked.Exchange(ref _saveQueued, 1) != 0)
        {
            Interlocked.Increment(ref _deferredSaveCoalescedRequests);
            App.Logger.Info("settings.save_deferred.coalesced", new
            {
                requests = DeferredSaveRequests,
                coalesced = DeferredSaveCoalescedRequests
            });
            return;
        }

        App.Logger.Info("settings.save_deferred.queued", new
        {
            requests = DeferredSaveRequests,
            coalesced = DeferredSaveCoalescedRequests
        });

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250).ConfigureAwait(false);
                Save();
                App.Logger.Info("settings.save_deferred.flushed", new
                {
                    requests = DeferredSaveRequests,
                    coalesced = DeferredSaveCoalescedRequests
                });
            }
            finally
            {
                Interlocked.Exchange(ref _saveQueued, 0);
            }
        });
    }

    public void FlushDeferredSave()
    {
        if (Volatile.Read(ref _saveQueued) == 0)
        {
            return;
        }

        Save();
        App.Logger.Info("settings.save_deferred.flush_on_shutdown", new
        {
            requests = DeferredSaveRequests,
            coalesced = DeferredSaveCoalescedRequests
        });
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

    private static void NormalizeSettings(AppSettings settings, string? rawJson = null)
    {
        settings.Heartbeat ??= new HeartbeatOptions();
        settings.Heartbeat.IntervalSeconds = Math.Max(0, settings.Heartbeat.IntervalSeconds);
        settings.Heartbeat.FailureThreshold = Math.Max(1, settings.Heartbeat.FailureThreshold);
        settings.Heartbeat.ConnectingThreshold = Math.Max(1, settings.Heartbeat.ConnectingThreshold);
        settings.HeartbeatIntervalSeconds = Math.Max(0, settings.HeartbeatIntervalSeconds);

        var hasLegacyInterval = false;
        var hasHeartbeatObject = false;
        var hasHeartbeatInterval = false;

        if (!string.IsNullOrWhiteSpace(rawJson))
        {
            try
            {
                using var document = JsonDocument.Parse(rawJson);
                var root = document.RootElement;
                hasLegacyInterval = root.TryGetProperty("heartbeatIntervalSeconds", out _);

                if (root.TryGetProperty("heartbeat", out var heartbeatElement) &&
                    heartbeatElement.ValueKind == JsonValueKind.Object)
                {
                    hasHeartbeatObject = true;
                    hasHeartbeatInterval = heartbeatElement.TryGetProperty("intervalSeconds", out _);
                }
            }
            catch (JsonException)
            {
                // Deserialization already succeeded; leave normalization on the object graph only.
            }
        }

        if (hasLegacyInterval && (!hasHeartbeatObject || !hasHeartbeatInterval))
        {
            settings.Heartbeat.IntervalSeconds = settings.HeartbeatIntervalSeconds;
            settings.Heartbeat.EnableHeartbeat = settings.HeartbeatIntervalSeconds > 0;
        }

        settings.HeartbeatIntervalSeconds = settings.Heartbeat.EnableHeartbeat
            ? settings.Heartbeat.IntervalSeconds
            : 0;
    }
}
