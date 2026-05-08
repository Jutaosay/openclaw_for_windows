// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Text.Json;
using OpenClaw.Helpers;
using OpenClaw.Models;

namespace OpenClaw.Services;

/// <summary>
/// Manages application settings persistence using JSON file storage.
/// Settings are stored in %LOCALAPPDATA%\OpenClaw\settings.json.
/// </summary>
public class ConfigurationService
{
    private static readonly string DefaultAppDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenClaw");

    private readonly string _appDataFolder;
    private readonly string _settingsFilePath;
    private readonly IAppLogger _logger;
    private readonly TimeSpan _deferredSaveDelay;
    private readonly Action<string, string> _writeAllText;

    private readonly object _lock = new();
    private int _saveQueued;
    private int _saveVersion;
    private int _deferredSaveRequests;
    private int _deferredSaveCoalescedRequests;

    public ConfigurationService()
        : this(DefaultAppDataFolder, NullAppLogger.Instance)
    {
    }

    public ConfigurationService(IAppLogger? logger)
        : this(DefaultAppDataFolder, logger)
    {
    }

    public ConfigurationService(
        string appDataFolder,
        IAppLogger? logger = null,
        TimeSpan? deferredSaveDelay = null,
        Action<string, string>? writeAllText = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataFolder);
        _appDataFolder = Path.GetFullPath(appDataFolder);
        _settingsFilePath = Path.Combine(_appDataFolder, "settings.json");
        _logger = logger ?? NullAppLogger.Instance;
        _deferredSaveDelay = deferredSaveDelay ?? TimeSpan.FromMilliseconds(250);
        _writeAllText = writeAllText ?? AtomicFileWriter.WriteAllText;
    }

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
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
                    if (settings is not null)
                    {
                        var settingsChanged = NormalizeSettings(settings, json);
                        Settings = settings;
                        if (settingsChanged)
                        {
                            Save();
                        }

                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to load settings: {ex.Message}");
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
                Directory.CreateDirectory(_appDataFolder);
                var json = JsonSerializer.Serialize(Settings, AppSettingsJsonContext.Default.AppSettings);
                _writeAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to save settings: {ex.Message}");
            }
        }
    }

    public void SaveDeferred()
    {
        Interlocked.Increment(ref _deferredSaveRequests);
        Interlocked.Increment(ref _saveVersion);

        if (Interlocked.CompareExchange(ref _saveQueued, 1, 0) != 0)
        {
            Interlocked.Increment(ref _deferredSaveCoalescedRequests);
            _logger.Info("settings.save_deferred.coalesced", new
            {
                requests = DeferredSaveRequests,
                coalesced = DeferredSaveCoalescedRequests
            });
            return;
        }

        _logger.Info("settings.save_deferred.queued", new
        {
            requests = DeferredSaveRequests,
            coalesced = DeferredSaveCoalescedRequests
        });

        _ = Task.Run(ProcessDeferredSaveQueueAsync);
    }

    private async Task ProcessDeferredSaveQueueAsync()
    {
        while (true)
        {
            var versionToFlush = Volatile.Read(ref _saveVersion);

            await Task.Delay(_deferredSaveDelay).ConfigureAwait(false);
            Save();
            _logger.Info("settings.save_deferred.flushed", new
            {
                requests = DeferredSaveRequests,
                coalesced = DeferredSaveCoalescedRequests
            });

            Interlocked.Exchange(ref _saveQueued, 0);

            if (Volatile.Read(ref _saveVersion) == versionToFlush)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _saveQueued, 1, 0) != 0)
            {
                return;
            }
        }
    }

    public void FlushDeferredSave()
    {
        if (Volatile.Read(ref _saveQueued) == 0)
        {
            return;
        }

        Save();
        _logger.Info("settings.save_deferred.flush_on_shutdown", new
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

    private static bool NormalizeSettings(AppSettings settings, string? rawJson = null)
    {
        var changed = false;

        if (settings.Environments is null)
        {
            settings.Environments = [];
            changed = true;
        }

        if (settings.RecoveryPolicy is null)
        {
            settings.RecoveryPolicy = new RecoveryPolicyOptions();
            changed = true;
        }

        if (settings.Heartbeat is null)
        {
            settings.Heartbeat = new HeartbeatOptions();
            changed = true;
        }

        if (settings.Diagnostics is null)
        {
            settings.Diagnostics = new DiagnosticsOptions();
            changed = true;
        }

        changed |= NormalizeWindowBounds(settings);

        var normalizedHeartbeatInterval = Math.Max(0, settings.Heartbeat.IntervalSeconds);
        changed |= normalizedHeartbeatInterval != settings.Heartbeat.IntervalSeconds;
        settings.Heartbeat.IntervalSeconds = normalizedHeartbeatInterval;

        var normalizedHeartbeatFailureThreshold = Math.Max(1, settings.Heartbeat.FailureThreshold);
        changed |= normalizedHeartbeatFailureThreshold != settings.Heartbeat.FailureThreshold;
        settings.Heartbeat.FailureThreshold = normalizedHeartbeatFailureThreshold;

        var normalizedHeartbeatConnectingThreshold = Math.Max(1, settings.Heartbeat.ConnectingThreshold);
        changed |= normalizedHeartbeatConnectingThreshold != settings.Heartbeat.ConnectingThreshold;
        settings.Heartbeat.ConnectingThreshold = normalizedHeartbeatConnectingThreshold;

        var normalizedLegacyHeartbeatInterval = Math.Max(0, settings.HeartbeatIntervalSeconds);
        changed |= normalizedLegacyHeartbeatInterval != settings.HeartbeatIntervalSeconds;
        settings.HeartbeatIntervalSeconds = normalizedLegacyHeartbeatInterval;

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
            changed |= settings.Heartbeat.IntervalSeconds != settings.HeartbeatIntervalSeconds;
            changed |= settings.Heartbeat.EnableHeartbeat != (settings.HeartbeatIntervalSeconds > 0);
            settings.Heartbeat.IntervalSeconds = settings.HeartbeatIntervalSeconds;
            settings.Heartbeat.EnableHeartbeat = settings.HeartbeatIntervalSeconds > 0;
        }

        var synchronizedHeartbeatInterval = settings.Heartbeat.EnableHeartbeat
            ? settings.Heartbeat.IntervalSeconds
            : 0;
        changed |= settings.HeartbeatIntervalSeconds != synchronizedHeartbeatInterval;
        settings.HeartbeatIntervalSeconds = synchronizedHeartbeatInterval;

        return changed;
    }

    private static bool NormalizeWindowBounds(AppSettings settings)
    {
        if (!WindowBoundsUtilities.HasPersistableSize(settings.WindowWidth, settings.WindowHeight) ||
            WindowBoundsUtilities.HasMinimizedSentinelPosition(settings.WindowLeft, settings.WindowTop))
        {
            settings.WindowWidth = WindowBoundsUtilities.DefaultWindowWidth;
            settings.WindowHeight = WindowBoundsUtilities.DefaultWindowHeight;
            settings.WindowLeft = -1;
            settings.WindowTop = -1;
            return true;
        }

        if (!WindowBoundsUtilities.HasSavedPosition(settings.WindowLeft, settings.WindowTop) &&
            (settings.WindowLeft != -1 || settings.WindowTop != -1))
        {
            settings.WindowLeft = -1;
            settings.WindowTop = -1;
            return true;
        }

        return false;
    }
}
