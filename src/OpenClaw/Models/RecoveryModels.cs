// Copyright (c) Lanstack @openclaw. All rights reserved.

namespace OpenClaw.Models;

/// <summary>
/// Represents the current recovery state of the hosted session.
/// </summary>
public enum RecoveryState
{
    /// <summary>
    /// No recovery in progress, session is healthy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Initial connection phase.
    /// </summary>
    Connecting,

    /// <summary>
    /// Session is ready and operational.
    /// </summary>
    Ready,

    /// <summary>
    /// Session is degraded, recovery recommended.
    /// </summary>
    Degraded,

    /// <summary>
    /// Reconnection in progress.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// Soft resync in progress (state reconciliation without full reload).
    /// </summary>
    Resyncing,

    /// <summary>
    /// Hard refresh in progress (full page reload).
    /// </summary>
    Refreshing,

    /// <summary>
    /// Authentication issue detected, user action required.
    /// </summary>
    AuthIssue,

    /// <summary>
    /// Recovery failed, manual intervention required.
    /// </summary>
    Failed,
}

/// <summary>
/// Represents the health status of a connection component.
/// </summary>
public enum HealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy,
}

/// <summary>
/// Snapshot of connection health at a point in time.
/// </summary>
public record ConnectionHealthSnapshot(
    /// <summary>
    /// Current recovery state.
    /// </summary>
    RecoveryState RecoveryState,

    /// <summary>
    /// Transport (HTTP/WSS) reachability status.
    /// </summary>
    HealthStatus TransportHealth,

    /// <summary>
    /// Gateway session health status.
    /// </summary>
    HealthStatus SessionHealth,

    /// <summary>
    /// Event stream consistency status.
    /// </summary>
    HealthStatus StreamHealth,

    /// <summary>
    /// Hosted UI availability status.
    /// </summary>
    HealthStatus HostedUiHealth,

    /// <summary>
    /// Last event sequence number observed (if available).
    /// </summary>
    long? LastEventSeq,

    /// <summary>
    /// Last state version observed (if available).
    /// </summary>
    string? LastStateVersion,

    /// <summary>
    /// Timestamp of last event observed.
    /// </summary>
    DateTimeOffset? LastEventAt,

    /// <summary>
    /// Timestamp of last successful heartbeat.
    /// </summary>
    DateTimeOffset? LastHeartbeatAt,

    /// <summary>
    /// Timestamp of last transport activity.
    /// </summary>
    DateTimeOffset? LastTransportActivityAt,

    /// <summary>
    /// Current environment name.
    /// </summary>
    string? EnvironmentName,

    /// <summary>
    /// Current gateway URL.
    /// </summary>
    string? GatewayUrl,

    /// <summary>
    /// Time spent in background (if applicable).
    /// </summary>
    TimeSpan? BackgroundDuration,

    /// <summary>
    /// Human-readable degradation reason (if any).
    /// </summary>
    string? DegradationReason
);

/// <summary>
/// Telemetry snapshot for recovery diagnostics.
/// </summary>
public record RecoveryTelemetrySnapshot(
    /// <summary>
    /// Total reconnect attempts in current session.
    /// </summary>
    int TotalReconnectAttempts,

    /// <summary>
    /// Total soft resync attempts in current session.
    /// </summary>
    int TotalSoftResyncAttempts,

    /// <summary>
    /// Total hard refresh attempts in current session.
    /// </summary>
    int TotalHardRefreshAttempts,

    /// <summary>
    /// Recent gap count (last 5 minutes).
    /// </summary>
    int RecentGapCount,

    /// <summary>
    /// Last recovery reason.
    /// </summary>
    string? LastRecoveryReason,

    /// <summary>
    /// Last recovery start time.
    /// </summary>
    DateTimeOffset? LastRecoveryStartedAt,

    /// <summary>
    /// Last successful recovery time.
    /// </summary>
    DateTimeOffset? LastSuccessfulRecoveryAt,

    /// <summary>
    /// Current recovery state.
    /// </summary>
    RecoveryState CurrentRecoveryState,

    /// <summary>
    /// Whether currently in background.
    /// </summary>
    bool IsInBackground,

    /// <summary>
    /// Time when app went to background (if applicable).
    /// </summary>
    DateTimeOffset? HiddenAt,

    /// <summary>
    /// Whether Cloudflare Tunnel is detected.
    /// </summary>
    bool IsTunnelDetected,

    /// <summary>
    /// Current heartbeat interval (seconds).
    /// </summary>
    int HeartbeatIntervalSeconds
);

/// <summary>
/// Represents a hosted session's current state.
/// </summary>
public record HostedSessionSnapshot(
    /// <summary>
    /// Whether the hosted UI is loaded and ready.
    /// </summary>
    bool IsUiReady,

    /// <summary>
    /// Whether the gateway session is established.
    /// </summary>
    bool IsSessionReady,

    /// <summary>
    /// Current page URI.
    /// </summary>
    string? CurrentUri,

    /// <summary>
    /// Current model identifier (if detected).
    /// </summary>
    string? CurrentModel,

    /// <summary>
    /// Whether the session is busy (streaming/generating).
    /// </summary>
    bool IsBusy,

    /// <summary>
    /// Current work state (idle/busy/unknown).
    /// </summary>
    string? WorkState,

    /// <summary>
    /// Authentication status.
    /// </summary>
    bool IsAuthenticated,

    /// <summary>
    /// Whether device pairing is required.
    /// </summary>
    bool IsPairingRequired,

    /// <summary>
    /// Whether origin was rejected.
    /// </summary>
    bool IsOriginRejected,

    /// <summary>
    /// Last known error message (if any).
    /// </summary>
    string? ErrorMessage
);
