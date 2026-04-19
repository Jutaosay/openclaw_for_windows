// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

/// <summary>
/// Central coordinator for hosted session recovery.
/// Manages reconnection, resync, and refresh decisions based on
/// transport health, session state, and event stream consistency.
/// </summary>
public sealed class ShellSessionCoordinator
{
    private RecoveryPolicyOptions _recoveryOptions = null!;
    private HeartbeatOptions _heartbeatOptions = null!;
    private DiagnosticsOptions _diagnosticsOptions = null!;
    private LoggingService _logger = null!;

    private WebViewService? _webViewService;
    private HostedUiBridge? _bridge;
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
    private bool _isRecoveryInProgress;
    private CancellationTokenSource? _recoveryCts;

    /// <summary>
    /// Raised when recovery state changes.
    /// </summary>
    public event Action<RecoveryState>? RecoveryStateChanged;

    /// <summary>
    /// Raised when a health snapshot is available.
    /// </summary>
    public event Action<ConnectionHealthSnapshot>? HealthSnapshotUpdated;

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

    /// <summary>
    /// Gets the latest health snapshot.
    /// </summary>
    public ConnectionHealthSnapshot GetHealthSnapshot() => new(
        _recoveryState,
        _transportHealth,
        _sessionHealth,
        _streamHealth,
        _hostedUiHealth,
        _lastEventSeq,
        _lastStateVersion,
        _lastEventAt,
        _lastHeartbeatAt,
        _lastTransportActivityAt,
        _currentEnvironmentName,
        _currentGatewayUrl,
        _backgroundDuration,
        _degradationReason
    );

    /// <summary>
    /// Gets the latest telemetry snapshot.
    /// </summary>
    public RecoveryTelemetrySnapshot GetTelemetrySnapshot() => new(
        _totalReconnectAttempts,
        _totalSoftResyncAttempts,
        _totalHardRefreshAttempts,
        _recentGapCount,
        _lastRecoveryReason,
        _lastRecoveryStartedAt,
        _lastSuccessfulRecoveryAt,
        _recoveryState,
        _isInBackground,
        _hiddenAt,
        IsTunnelDetected(),
        _heartbeatOptions.IntervalSeconds
    );

    /// <summary>
    /// Attaches the coordinator to the required services.
    /// </summary>
    public Task AttachAsync(
        WebViewService webViewService,
        HostedUiBridge bridge,
        RecoveryPolicyOptions? recoveryOptions = null,
        HeartbeatOptions? heartbeatOptions = null,
        DiagnosticsOptions? diagnosticsOptions = null)
    {
        if (_webViewService is not null)
        {
            _webViewService.ConnectionStateChanged -= OnConnectionStateChanged;
            _webViewService.NavigationErrorOccurred -= OnNavigationError;
            _webViewService.NavigationCompleted -= OnNavigationCompleted;
            _webViewService.ControlUiSnapshotUpdated -= OnHostedUiStateUpdated;
            _webViewService.HeartbeatObserved -= OnHeartbeatObserved;
            _webViewService.HeartbeatFailed -= OnHeartbeatFailed;
        }

        if (_bridge is not null)
        {
            _bridge.SessionReady -= OnSessionReady;
            _bridge.EventGapDetected -= OnEventGapDetected;
        }

        _webViewService = webViewService;
        _bridge = bridge;
        _recoveryOptions = recoveryOptions ?? App.Configuration.Settings.RecoveryPolicy;
        _heartbeatOptions = heartbeatOptions ?? App.Configuration.Settings.Heartbeat;
        _diagnosticsOptions = diagnosticsOptions ?? App.Configuration.Settings.Diagnostics;

        // Wire up events
        _webViewService.ConnectionStateChanged += OnConnectionStateChanged;
        _webViewService.NavigationErrorOccurred += OnNavigationError;
        _webViewService.NavigationCompleted += OnNavigationCompleted;
        _webViewService.ControlUiSnapshotUpdated += OnHostedUiStateUpdated;
        _webViewService.HeartbeatObserved += OnHeartbeatObserved;
        _webViewService.HeartbeatFailed += OnHeartbeatFailed;
        _bridge.SessionReady += OnSessionReady;
        _bridge.EventGapDetected += OnEventGapDetected;

        _logger = App.Logger;
        _logger.Info("ShellSessionCoordinator attached.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the host window goes to background.
    /// </summary>
    public void OnHostHidden()
    {
        _isInBackground = true;
        _hiddenAt = DateTimeOffset.Now;
        _logger.Info("host.hidden", new { environment = _currentEnvironmentName, at = _hiddenAt });
        TelemetryUpdated?.Invoke(GetTelemetrySnapshot());
    }

    /// <summary>
    /// Called when the host window returns to foreground.
    /// </summary>
    public async Task OnHostVisibleAsync()
    {
        if (!_isInBackground)
        {
            return;
        }

        _isInBackground = false;
        var visibleAt = DateTimeOffset.Now;

        if (_hiddenAt.HasValue)
        {
            _backgroundDuration = visibleAt - _hiddenAt.Value;
            _logger.Info("host.visible", new
            {
                environment = _currentEnvironmentName,
                hiddenAt = _hiddenAt,
                visibleAt,
                durationSeconds = _backgroundDuration.Value.TotalSeconds
            });
        }

        _hiddenAt = null;

        // Decide if recovery is needed
        if (_recoveryOptions.EnableBackgroundResume &&
            _backgroundDuration.HasValue &&
            _backgroundDuration.Value.TotalSeconds >= _recoveryOptions.BackgroundResumeThresholdSeconds)
        {
            var requiresReconnect = await RequiresBackgroundReconnectAsync();
            _logger.Info("recovery.start", new
            {
                reason = "background_resume",
                durationSeconds = _backgroundDuration.Value.TotalSeconds,
                requiresReconnect
            });

            if (requiresReconnect)
            {
                await RequestReconnectAsync("Background resume threshold exceeded");
            }
            else
            {
                _logger.Info("recovery.skipped", new { reason = "background_resume_session_still_healthy" });
            }
        }

        _backgroundDuration = null;
        TelemetryUpdated?.Invoke(GetTelemetrySnapshot());
    }

    /// <summary>
    /// Called when WebView reports navigation completed.
    /// </summary>
    private void OnNavigationCompleted(string? uri)
    {
        _lastTransportActivityAt = DateTimeOffset.Now;
        _transportHealth = HealthStatus.Healthy;
        _hostedUiHealth = HealthStatus.Degraded;

        _logger.Info("webview.navigation.ok", new { uri });
        HealthSnapshotUpdated?.Invoke(GetHealthSnapshot());
    }

    /// <summary>
    /// Called when WebView reports navigation failed.
    /// </summary>
    private async void OnNavigationFailed(string error)
    {
        _transportHealth = HealthStatus.Unhealthy;
        _logger.Warning("webview.navigation.fail", new { error });

        if (_recoveryState is RecoveryState.Ready or RecoveryState.Degraded)
        {
            await RequestReconnectAsync($"Navigation failed: {error}");
        }
    }

    /// <summary>
    /// Called when hosted UI reports ready state.
    /// </summary>
    private void OnHostedUiStateUpdated(ControlUiProbeSnapshot snapshot)
    {
        _lastTransportActivityAt = DateTimeOffset.Now;
        _lastEventAt = _lastTransportActivityAt;

        // Update hosted UI health
        _hostedUiHealth = snapshot.Phase switch
        {
            ControlUiPhase.Connected => HealthStatus.Healthy,
            ControlUiPhase.AuthRequired or ControlUiPhase.PairingRequired or ControlUiPhase.OriginRejected => HealthStatus.Unhealthy,
            ControlUiPhase.GatewayError => HealthStatus.Degraded,
            _ => HealthStatus.Degraded,
        };

        // Update session health based on phase
        _sessionHealth = snapshot.Phase switch
        {
            ControlUiPhase.Connected => HealthStatus.Healthy,
            ControlUiPhase.GatewayConnecting or ControlUiPhase.PageLoaded => HealthStatus.Degraded,
            _ => HealthStatus.Unhealthy,
        };

        _streamHealth = snapshot.Phase switch
        {
            ControlUiPhase.Connected => HealthStatus.Healthy,
            ControlUiPhase.GatewayConnecting or ControlUiPhase.PageLoaded => HealthStatus.Degraded,
            _ => _streamHealth
        };

        _logger.Info("hosted_ui.state", new
        {
            phase = snapshot.Phase,
            summary = snapshot.Summary,
            shellDetected = snapshot.ShellDetected
        });

        switch (snapshot.Phase)
        {
            case ControlUiPhase.Connected when _recoveryState is RecoveryState.Connecting or RecoveryState.Reconnecting or RecoveryState.Resyncing:
                _degradationReason = null;
                SetRecoveryState(RecoveryState.Ready);
                _lastSuccessfulRecoveryAt = DateTimeOffset.Now;
                ResetEscalationCounters();
                break;
            case ControlUiPhase.AuthRequired:
            case ControlUiPhase.PairingRequired:
            case ControlUiPhase.OriginRejected:
                _degradationReason = snapshot.DetailOrSummary;
                SetRecoveryState(RecoveryState.AuthIssue);
                break;
        }

        HealthSnapshotUpdated?.Invoke(GetHealthSnapshot());
    }

    /// <summary>
    /// Called when hosted UI reports session ready.
    /// </summary>
    private void OnSessionReady(SessionReadyEventArgs args)
    {
        _sessionHealth = HealthStatus.Healthy;
        _hostedUiHealth = HealthStatus.Healthy;
        _streamHealth = HealthStatus.Healthy;
        _degradationReason = null;
        ResetEscalationCounters();

        if (_recoveryState is RecoveryState.Connecting or RecoveryState.Reconnecting)
        {
            SetRecoveryState(RecoveryState.Ready);
            _lastSuccessfulRecoveryAt = DateTimeOffset.Now;
            _logger.Info("session.ready", new { args.Model, args.Uri });
        }

        HealthSnapshotUpdated?.Invoke(GetHealthSnapshot());
        TelemetryUpdated?.Invoke(GetTelemetrySnapshot());
    }

    /// <summary>
    /// Called when an event gap is detected.
    /// </summary>
    private async void OnEventGapDetected(EventGapEventArgs args)
    {
        if (_isInBackground)
        {
            _logger.Info("stream.gap.ignored", new
            {
                reason = "background",
                expectedSeq = args.ExpectedSeq,
                gotSeq = args.GotSeq
            });
            return;
        }

        _recentGapCount++;
        _lastEventSeq = args.GotSeq;
        _lastStateVersion = args.CurrentStateVersion;
        _streamHealth = HealthStatus.Degraded;

        _logger.Warning("stream.gap.detected", new
        {
            expectedSeq = args.ExpectedSeq,
            gotSeq = args.GotSeq,
            gapSize = args.GotSeq - args.ExpectedSeq
        });

        var preferredGapRecovery = await GetPreferredGapRecoveryAsync();

        // If the session still looks alive, prefer a lightweight resync to avoid
        // tearing down the user's current page and in-progress input.
        if (preferredGapRecovery == GapRecoveryAction.None)
        {
            HealthSnapshotUpdated?.Invoke(GetHealthSnapshot());
            TelemetryUpdated?.Invoke(GetTelemetrySnapshot());
            return;
        }

        if (preferredGapRecovery == GapRecoveryAction.SoftResync &&
            _softResyncAttempts < _recoveryOptions.MaxSoftResyncAttempts)
        {
            await RequestSoftResyncAsync($"Event gap detected while session remained alive (seq {args.ExpectedSeq} -> {args.GotSeq})");
        }
        else if (_reconnectAttempts < _recoveryOptions.MaxReconnectAttempts)
        {
            await RequestReconnectAsync($"Event gap detected (seq {args.ExpectedSeq} -> {args.GotSeq})");
        }
        else if (_softResyncAttempts < _recoveryOptions.MaxSoftResyncAttempts)
        {
            await RequestSoftResyncAsync($"Event gap persists after {_reconnectAttempts} reconnects");
        }
        else
        {
            await RequestHardRefreshAsync($"Event gap persists after soft resync attempts");
        }

        HealthSnapshotUpdated?.Invoke(GetHealthSnapshot());
        TelemetryUpdated?.Invoke(GetTelemetrySnapshot());
    }

    /// <summary>
    /// Called when connection state changes.
    /// </summary>
    private void OnConnectionStateChanged(ConnectionState state)
    {
        _transportHealth = state switch
        {
            ConnectionState.Connected => HealthStatus.Healthy,
            ConnectionState.Loading or ConnectionState.GatewayConnecting => HealthStatus.Degraded,
            ConnectionState.Reconnecting => HealthStatus.Degraded,
            ConnectionState.AuthFailed or ConnectionState.Error => HealthStatus.Unhealthy,
            _ => HealthStatus.Unknown,
        };

        _logger.Info($"connection.state.{state.ToString().ToLower()}", new { state });
        HealthSnapshotUpdated?.Invoke(GetHealthSnapshot());
    }

    /// <summary>
    /// Called when a navigation error occurs.
    /// </summary>
    private void OnNavigationError(string message)
    {
        _logger.Error("navigation.error", new { message });
    }

    /// <summary>
    /// Requests a reconnection to the gateway.
    /// </summary>
    public async Task RequestReconnectAsync(string reason)
    {
        if (_isRecoveryInProgress)
        {
            _logger.Info("recovery.throttled", new { reason, current = _recoveryState });
            return;
        }

        if (ShouldThrottleHardRefresh() && _recoveryState == RecoveryState.Refreshing)
        {
            _logger.Warning("recovery.throttled", new { reason, lastRefreshAt = _lastHardRefreshAt });
            return;
        }

        _isRecoveryInProgress = true;
        _recoveryCts?.Cancel();
        _recoveryCts = new CancellationTokenSource();
        _lastRecoveryStartedAt = DateTimeOffset.Now;
        _lastRecoveryReason = reason;
        _reconnectAttempts++;
        _totalReconnectAttempts++;

        SetRecoveryState(RecoveryState.Reconnecting);
        _logger.Info("recovery.reconnect.start", new { reason, attempt = _reconnectAttempts });

        try
        {
            var delay = CalculateReconnectDelay();
            await Task.Delay(delay, _recoveryCts.Token);

            if (_webViewService is not null)
            {
                var handledInPage = false;
                if (_bridge is not null)
                {
                    handledInPage = await _bridge.NotifyReconnectIntentAsync();
                    handledInPage = await _bridge.RequestSessionRefreshAsync() || handledInPage;
                }

                if (handledInPage)
                {
                    await Task.Delay(750, _recoveryCts.Token);
                    var snapshot = await _webViewService.InspectControlUiStateAsync();
                    if (TryResolveReloadFallback(snapshot, reason, "post_in_page_reconnect"))
                    {
                        return;
                    }
                }

                var preReloadSnapshot = await _webViewService.InspectControlUiStateAsync();
                if (TryResolveReloadFallback(preReloadSnapshot, reason, "pre_reload"))
                {
                    return;
                }

                _webViewService.Reload();
                _lastTransportActivityAt = DateTimeOffset.Now;
                _logger.Info("recovery.reconnect.reload", new { attempt = _reconnectAttempts, handledInPage });
                SetRecoveryState(RecoveryState.Connecting);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Info("recovery.cancelled", new { reason });
        }
        catch (Exception ex)
        {
            _logger.Error("recovery.reconnect.fail", new { ex.Message });
            SetRecoveryState(RecoveryState.Degraded);
            _degradationReason = ex.Message;
        }
        finally
        {
            _isRecoveryInProgress = false;
            TelemetryUpdated?.Invoke(GetTelemetrySnapshot());
        }
    }

    /// <summary>
    /// Requests a soft resync (state reconciliation without full reload).
    /// </summary>
    public async Task RequestSoftResyncAsync(string reason)
    {
        if (_isRecoveryInProgress || _bridge is null)
        {
            return;
        }

        _isRecoveryInProgress = true;
        _softResyncAttempts++;
        _totalSoftResyncAttempts++;
        _lastRecoveryStartedAt = DateTimeOffset.Now;
        _lastRecoveryReason = reason;

        SetRecoveryState(RecoveryState.Resyncing);
        _logger.Info("recovery.soft_resync.start", new { reason, attempt = _softResyncAttempts });

        try
        {
            var handled = await _bridge.RequestLightweightSyncAsync();
            handled = await _bridge.RequestRecentMessagesAsync() || handled;

            if (!handled)
            {
                _logger.Warning("recovery.soft_resync.unsupported", new { attempt = _softResyncAttempts });
                SetRecoveryState(RecoveryState.Degraded);
                return;
            }

            await Task.Delay(1500);

            if (_webViewService is not null)
            {
                var snapshot = await _webViewService.InspectControlUiStateAsync();
                if (IsSessionAlive(snapshot))
                {
                    _streamHealth = HealthStatus.Healthy;
                }
            }

            if (_streamHealth == HealthStatus.Healthy)
            {
                _logger.Info("recovery.soft_resync.ok", new { attempt = _softResyncAttempts });
                SetRecoveryState(RecoveryState.Ready);
                _lastSuccessfulRecoveryAt = DateTimeOffset.Now;
            }
            else
            {
                _logger.Warning("recovery.soft_resync.fail", new { attempt = _softResyncAttempts, streamHealth = _streamHealth });
                SetRecoveryState(RecoveryState.Degraded);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("recovery.soft_resync.fail", new { ex.Message });
            SetRecoveryState(RecoveryState.Degraded);
        }
        finally
        {
            _isRecoveryInProgress = false;
            TelemetryUpdated?.Invoke(GetTelemetrySnapshot());
        }
    }

    /// <summary>
    /// Requests a hard refresh (full page reload).
    /// </summary>
    public async Task RequestHardRefreshAsync(string reason)
    {
        if (_isRecoveryInProgress || _webViewService is null)
        {
            return;
        }

        if (ShouldThrottleHardRefresh())
        {
            _logger.Warning("recovery.hard_refresh.throttled", new { reason, lastRefreshAt = _lastHardRefreshAt });
            return;
        }

        var webViewService = _webViewService;
        if (webViewService is null)
        {
            return;
        }

        _isRecoveryInProgress = true;
        _hardRefreshAttempts++;
        _totalHardRefreshAttempts++;
        _lastHardRefreshAt = DateTimeOffset.Now;
        _lastRecoveryStartedAt = DateTimeOffset.Now;
        _lastRecoveryReason = reason;

        SetRecoveryState(RecoveryState.Refreshing);
        _logger.Info("recovery.hard_refresh", new { reason, attempt = _hardRefreshAttempts });

        try
        {
            var snapshot = await webViewService.InspectControlUiStateAsync();
            if (TryResolveReloadFallback(snapshot, reason, "hard_refresh"))
            {
                return;
            }

            webViewService.Reload();
            await Task.Delay(1000);
            SetRecoveryState(RecoveryState.Connecting);
        }
        catch (Exception ex)
        {
            _logger.Error("recovery.hard_refresh.fail", new { ex.Message });
            SetRecoveryState(RecoveryState.Failed);
        }
        finally
        {
            _isRecoveryInProgress = false;
            TelemetryUpdated?.Invoke(GetTelemetrySnapshot());
        }
    }

    /// <summary>
    /// Updates heartbeat observation.
    /// </summary>
    private void OnHeartbeatObserved(HeartbeatProbeResult result)
    {
        _lastHeartbeatAt = DateTimeOffset.Now;

        if (result.Status == HeartbeatProbeStatus.Healthy)
        {
            _transportHealth = HealthStatus.Healthy;
        }
        else if (result.Status == HeartbeatProbeStatus.SessionBlocked)
        {
            _sessionHealth = HealthStatus.Unhealthy;
        }
        else if (result.Status == HeartbeatProbeStatus.Failure)
        {
            _transportHealth = HealthStatus.Unhealthy;
        }
        else if (result.Status == HeartbeatProbeStatus.Connecting)
        {
            _transportHealth = HealthStatus.Degraded;
        }

        HealthSnapshotUpdated?.Invoke(GetHealthSnapshot());
    }

    private async void OnHeartbeatFailed(string message)
    {
        if (_isInBackground)
        {
            _logger.Info("heartbeat.recovery.deferred", new { message, reason = "background" });
            return;
        }

        _logger.Warning("heartbeat.recovery.requested", new { message });
        await RequestReconnectAsync($"Heartbeat recovery requested: {message}");
    }

    private async Task<GapRecoveryAction> GetPreferredGapRecoveryAsync()
    {
        if (_webViewService is null)
        {
            return GapRecoveryAction.Reconnect;
        }

        try
        {
            var snapshot = await _webViewService.InspectControlUiStateAsync();
            if (snapshot.Phase is ControlUiPhase.AuthRequired or ControlUiPhase.PairingRequired or ControlUiPhase.OriginRejected)
            {
                _degradationReason = snapshot.DetailOrSummary;
                SetRecoveryState(RecoveryState.AuthIssue);
                return GapRecoveryAction.None;
            }

            return snapshot.InputFocused || IsSessionAlive(snapshot)
                ? GapRecoveryAction.SoftResync
                : GapRecoveryAction.Reconnect;
        }
        catch (Exception ex)
        {
            _logger.Warning("stream.gap.inspect_failed", new { ex.Message });
            return GapRecoveryAction.Reconnect;
        }
    }

    private async Task<bool> RequiresBackgroundReconnectAsync()
    {
        if (_webViewService is null)
        {
            return true;
        }

        try
        {
            var snapshot = await _webViewService.InspectControlUiStateAsync();
            if (snapshot.Phase == ControlUiPhase.Unavailable)
            {
                return true;
            }

            if (snapshot.Phase is ControlUiPhase.AuthRequired or ControlUiPhase.PairingRequired or ControlUiPhase.OriginRejected)
            {
                SetRecoveryState(RecoveryState.AuthIssue);
                _degradationReason = snapshot.DetailOrSummary;
                return false;
            }

            return !IsSessionAlive(snapshot);
        }
        catch (Exception ex)
        {
            _logger.Warning("recovery.background_resume.inspect_failed", new { ex.Message });
            return true;
        }
    }

    private static bool IsSessionAlive(ControlUiProbeSnapshot snapshot)
    {
        return snapshot.Phase is ControlUiPhase.Connected
            or ControlUiPhase.GatewayConnecting
            or ControlUiPhase.PageLoaded;
    }

    private bool TryResolveReloadFallback(ControlUiProbeSnapshot snapshot, string reason, string stage)
    {
        if (snapshot.Phase is ControlUiPhase.AuthRequired or ControlUiPhase.PairingRequired or ControlUiPhase.OriginRejected)
        {
            _degradationReason = snapshot.DetailOrSummary;
            SetRecoveryState(RecoveryState.AuthIssue);
            _logger.Warning("recovery.reload.skipped", new
            {
                reason,
                stage,
                phase = snapshot.Phase,
                detail = snapshot.DetailOrSummary
            });
            return true;
        }

        if (snapshot.InputFocused)
        {
            _degradationReason = "Automatic reload deferred while a text input is focused.";
            SetRecoveryState(snapshot.Phase == ControlUiPhase.Connected ? RecoveryState.Ready : RecoveryState.Degraded);
            _logger.Info("recovery.reload.deferred", new
            {
                reason,
                stage,
                phase = snapshot.Phase,
                inputFocused = true
            });
            return true;
        }

        if (!IsSessionAlive(snapshot))
        {
            return false;
        }

        _logger.Info("recovery.reconnect.soft_ok", new
        {
            reason,
            stage,
            attempt = _reconnectAttempts,
            snapshot = snapshot.Phase.ToString()
        });

        if (snapshot.Phase == ControlUiPhase.Connected)
        {
            _degradationReason = null;
            _lastSuccessfulRecoveryAt = DateTimeOffset.Now;
            ResetEscalationCounters();
            SetRecoveryState(RecoveryState.Ready);
        }
        else
        {
            SetRecoveryState(RecoveryState.Connecting);
        }

        return true;
    }

    private void ResetEscalationCounters()
    {
        _reconnectAttempts = 0;
        _softResyncAttempts = 0;
        _hardRefreshAttempts = 0;
        _recentGapCount = 0;
    }

    private void SetRecoveryState(RecoveryState newState)
    {
        if (_recoveryState != newState)
        {
            _recoveryState = newState;
            RecoveryStateChanged?.Invoke(newState);
            _logger.Info($"recovery.state.{newState.ToString().ToLower()}", new { newState });
        }
    }

    private TimeSpan CalculateReconnectDelay()
    {
        var baseDelay = _recoveryOptions.ReconnectDelayMs;
        var backoff = Math.Pow(_recoveryOptions.ReconnectBackoffMultiplier, _reconnectAttempts - 1);
        var delay = baseDelay * backoff;
        delay = Math.Min(delay, _recoveryOptions.MaxReconnectDelayMs);
        return TimeSpan.FromMilliseconds(delay);
    }

    private bool ShouldThrottleHardRefresh()
    {
        if (_lastHardRefreshAt is null)
        {
            return false;
        }

        var elapsed = DateTimeOffset.Now - _lastHardRefreshAt.Value;
        return elapsed.TotalSeconds < _recoveryOptions.HardRefreshCooldownSeconds;
    }

    private bool IsTunnelDetected()
    {
        // Simplified: check if gateway URL contains Cloudflare indicators
        if (string.IsNullOrEmpty(_currentGatewayUrl))
        {
            return false;
        }

        return _currentGatewayUrl.Contains("cloudflare") ||
               _currentGatewayUrl.Contains("workers.dev") ||
               _currentGatewayUrl.Contains("trycloudflare.com");
    }

    /// <summary>
    /// Sets the current environment context.
    /// </summary>
    public void SetEnvironment(string environmentName, string gatewayUrl)
    {
        _currentEnvironmentName = environmentName;
        _currentGatewayUrl = gatewayUrl;
        _logger.Info("environment.changed", new { environmentName, gatewayUrl });
    }

    /// <summary>
    /// Resets all recovery counters and state.
    /// </summary>
    public void Reset()
    {
        _reconnectAttempts = 0;
        _softResyncAttempts = 0;
        _hardRefreshAttempts = 0;
        _recentGapCount = 0;
        _recoveryState = RecoveryState.Connecting;
        _transportHealth = HealthStatus.Unknown;
        _sessionHealth = HealthStatus.Unknown;
        _streamHealth = HealthStatus.Unknown;
        _hostedUiHealth = HealthStatus.Unknown;
        _degradationReason = null;
        _lastRecoveryReason = null;

        _logger.Info("recovery.reset");
        RecoveryStateChanged?.Invoke(_recoveryState);
        HealthSnapshotUpdated?.Invoke(GetHealthSnapshot());
        TelemetryUpdated?.Invoke(GetTelemetrySnapshot());
    }

    /// <summary>
    /// Cleans up coordinator resources.
    /// </summary>
    public void Dispose()
    {
        if (_webViewService is not null)
        {
            _webViewService.ConnectionStateChanged -= OnConnectionStateChanged;
            _webViewService.NavigationErrorOccurred -= OnNavigationError;
            _webViewService.NavigationCompleted -= OnNavigationCompleted;
            _webViewService.ControlUiSnapshotUpdated -= OnHostedUiStateUpdated;
            _webViewService.HeartbeatObserved -= OnHeartbeatObserved;
            _webViewService.HeartbeatFailed -= OnHeartbeatFailed;
        }

        if (_bridge is not null)
        {
            _bridge.SessionReady -= OnSessionReady;
            _bridge.EventGapDetected -= OnEventGapDetected;
        }

        _recoveryCts?.Cancel();
        _recoveryCts?.Dispose();
    }

    private enum GapRecoveryAction
    {
        None,
        SoftResync,
        Reconnect,
    }
}
