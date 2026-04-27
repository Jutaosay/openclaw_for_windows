// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Text.Json.Serialization;

namespace OpenClaw.Models;

/// <summary>
/// Connection recovery policy configuration.
/// Controls reconnection, resync, and refresh behavior.
/// </summary>
public class RecoveryPolicyOptions
{
    /// <summary>
    /// Gets or sets whether background resume recovery is enabled.
    /// Default is true.
    /// </summary>
    public bool EnableBackgroundResume { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum background duration (seconds) to trigger resume recovery.
    /// If the app returns to foreground after being hidden for less than this, no recovery runs.
    /// Default is 10 seconds.
    /// </summary>
    public int BackgroundResumeThresholdSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of reconnect attempts before escalating to soft resync.
    /// Default is 3.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of soft resync attempts before escalating to hard refresh.
    /// Default is 2.
    /// </summary>
    public int MaxSoftResyncAttempts { get; set; } = 2;

    /// <summary>
    /// Gets or sets the idle suspicion timeout (seconds).
    /// If no event observed for this duration, connection is considered degraded.
    /// Default is 120 seconds.
    /// </summary>
    public int EventIdleSuspicionSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the transport idle suspicion timeout (seconds).
    /// If transport shows no activity for this duration, reconnect is recommended.
    /// Default is 60 seconds.
    /// </summary>
    public int TransportIdleSuspicionSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the delay before first reconnect attempt (milliseconds).
    /// Default is 1200ms.
    /// </summary>
    public int ReconnectDelayMs { get; set; } = 1200;

    /// <summary>
    /// Gets or sets the backoff multiplier for reconnect attempts.
    /// Each retry waits: previousDelay * backoff.
    /// Default is 2.0 (exponential backoff).
    /// </summary>
    public double ReconnectBackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the maximum reconnect delay (milliseconds).
    /// Default is 45000ms (45 seconds).
    /// </summary>
    public int MaxReconnectDelayMs { get; set; } = 45000;

    /// <summary>
    /// Gets or sets the minimum time between hard refresh attempts (seconds).
    /// Prevents refresh thrashing.
    /// Default is 75 seconds.
    /// </summary>
    public int HardRefreshCooldownSeconds { get; set; } = 75;
}

/// <summary>
/// Heartbeat configuration options.
/// </summary>
public class HeartbeatOptions
{
    /// <summary>
    /// Gets or sets whether heartbeat probing is enabled.
    /// Default is true.
    /// </summary>
    public bool EnableHeartbeat { get; set; } = true;

    /// <summary>
    /// Gets or sets the heartbeat probe interval in seconds.
    /// 0 disables heartbeat.
    /// Default is 45s.
    /// </summary>
    public int IntervalSeconds { get; set; } = 45;

    /// <summary>
    /// Gets or sets the number of consecutive heartbeat failures before triggering recovery.
    /// Default is 2.
    /// </summary>
    public int FailureThreshold { get; set; } = 2;

    /// <summary>
    /// Gets or sets the number of consecutive "connecting" states before triggering recovery.
    /// Default is 4.
    /// </summary>
    public int ConnectingThreshold { get; set; } = 4;
}

/// <summary>
/// Diagnostics configuration options.
/// </summary>
public class DiagnosticsOptions
{
    /// <summary>
    /// Gets or sets whether verbose recovery logging is enabled.
    /// When true, every recovery event is logged with full details.
    /// Default is false (summary only).
    /// </summary>
    public bool EnableVerboseRecoveryLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to collect telemetry snapshots periodically.
    /// Default is true.
    /// </summary>
    public bool EnableTelemetryCollection { get; set; } = true;

    /// <summary>
    /// Gets or sets the telemetry collection interval in seconds.
    /// Default is 60 seconds.
    /// </summary>
    public int TelemetryIntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Source generation context for recovery options serialization.
/// </summary>
[JsonSerializable(typeof(RecoveryPolicyOptions))]
[JsonSerializable(typeof(HeartbeatOptions))]
[JsonSerializable(typeof(DiagnosticsOptions))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class RecoveryOptionsJsonContext : JsonSerializerContext
{
}
