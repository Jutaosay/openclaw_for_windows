// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

/// <summary>
/// Central coordinator for hosted session recovery.
/// Manages reconnection, resync, and refresh decisions based on
/// transport health, session state, and event stream consistency.
/// </summary>
public sealed partial class ShellSessionCoordinator
{
    private RecoveryPolicyOptions _recoveryOptions;
    private HeartbeatOptions _heartbeatOptions;
    private LoggingService _logger;

    private IShellSessionWebView? _webViewService;
    private IShellSessionBridge? _bridge;
    private string? _currentEnvironmentName;
    private string? _currentGatewayUrl;

    // Recovery state
    private RecoveryState _recoveryState = RecoveryState.Connecting;
    private int _reconnectAttempts;
    private int _softResyncAttempts;
    private int _hardRefreshAttempts;
    private int _recentGapCount;
    private DateTimeOffset? _lastRecoveryStartedAt;
    private DateTimeOffset? _lastSuccessfulRecoveryAt;
    private DateTimeOffset? _lastHardRefreshAt;
    private DateTimeOffset? _hiddenAt;
    private TimeSpan? _backgroundDuration;
    private string? _lastRecoveryReason;
    private string? _degradationReason;
    private bool _isInBackground;

    // Telemetry
    private int _totalReconnectAttempts;
    private int _totalSoftResyncAttempts;
    private int _totalHardRefreshAttempts;
    private int _totalWebViewRecreations;
    private int _mergedWebViewRecreationRequests;
    private int _totalControlUiInspectionRequests;
    private int _cachedControlUiInspectionRequests;
    private int _coalescedControlUiInspectionRequests;
    private int _deferredSaveRequests;
    private int _deferredSaveCoalescedRequests;
    private int _heartbeatRecoveryRequests;
    private string? _lastInstrumentationEvent;

    // Health tracking
    private HealthStatus _transportHealth = HealthStatus.Unknown;
    private HealthStatus _sessionHealth = HealthStatus.Unknown;
    private HealthStatus _streamHealth = HealthStatus.Unknown;
    private HealthStatus _hostedUiHealth = HealthStatus.Unknown;
    private long? _lastEventSeq;
    private string? _lastStateVersion;
    private DateTimeOffset? _lastEventAt;
    private DateTimeOffset? _lastHeartbeatAt;
    private DateTimeOffset? _lastTransportActivityAt;

    // Throttling
    private readonly object _recoveryGate = new();
    private bool _isRecoveryInProgress;
    private CancellationTokenSource? _recoveryCts;

    /// <summary>
    /// Raised when recovery state changes.
    /// </summary>
    public event Action<RecoveryState>? RecoveryStateChanged;

    /// <summary>
    /// Raised when recovery telemetry is updated.
    /// </summary>
    public event Action<RecoveryTelemetrySnapshot>? TelemetryUpdated;

    /// <summary>
    /// Gets the current recovery state.
    /// </summary>
    public RecoveryState CurrentRecoveryState => _recoveryState;

    /// <summary>
    /// Gets whether recovery is currently in progress.
    /// </summary>
    public bool IsRecoveryInProgress => _isRecoveryInProgress;

    /// <summary>
    /// Gets the current environment name.
    /// </summary>
    public string? CurrentEnvironmentName => _currentEnvironmentName;

    /// <summary>
    /// Gets the current gateway URL.
    /// </summary>
    public string? CurrentGatewayUrl => _currentGatewayUrl;

    public ShellSessionCoordinator()
    {
        _recoveryOptions = App.Configuration.Settings.RecoveryPolicy;
        _heartbeatOptions = App.Configuration.Settings.Heartbeat;
        _logger = App.Logger;
    }

    private enum GapRecoveryAction
    {
        None,
        SoftResync,
        Reconnect,
    }

    internal void UpdateInstrumentation(
        int? totalWebViewRecreations = null,
        int? mergedWebViewRecreationRequests = null,
        int? totalControlUiInspectionRequests = null,
        int? cachedControlUiInspectionRequests = null,
        int? coalescedControlUiInspectionRequests = null,
        int? deferredSaveRequests = null,
        int? deferredSaveCoalescedRequests = null,
        int? heartbeatRecoveryRequests = null,
        string? lastInstrumentationEvent = null)
    {
        if (totalWebViewRecreations.HasValue)
        {
            _totalWebViewRecreations = totalWebViewRecreations.Value;
        }

        if (mergedWebViewRecreationRequests.HasValue)
        {
            _mergedWebViewRecreationRequests = mergedWebViewRecreationRequests.Value;
        }

        if (totalControlUiInspectionRequests.HasValue)
        {
            _totalControlUiInspectionRequests = totalControlUiInspectionRequests.Value;
        }

        if (cachedControlUiInspectionRequests.HasValue)
        {
            _cachedControlUiInspectionRequests = cachedControlUiInspectionRequests.Value;
        }

        if (coalescedControlUiInspectionRequests.HasValue)
        {
            _coalescedControlUiInspectionRequests = coalescedControlUiInspectionRequests.Value;
        }

        if (deferredSaveRequests.HasValue)
        {
            _deferredSaveRequests = deferredSaveRequests.Value;
        }

        if (deferredSaveCoalescedRequests.HasValue)
        {
            _deferredSaveCoalescedRequests = deferredSaveCoalescedRequests.Value;
        }

        if (heartbeatRecoveryRequests.HasValue)
        {
            _heartbeatRecoveryRequests = heartbeatRecoveryRequests.Value;
        }

        if (!string.IsNullOrWhiteSpace(lastInstrumentationEvent))
        {
            _lastInstrumentationEvent = lastInstrumentationEvent;
        }

        PublishTelemetry();
    }
}
