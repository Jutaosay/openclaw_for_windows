// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public sealed partial class ShellSessionCoordinator
{
    /// <summary>
    /// Called when the host window goes to background.
    /// </summary>
    public void OnHostHidden()
    {
        _isInBackground = true;
        _hiddenAt = DateTimeOffset.Now;
        _logger.Info("host.hidden", new { environment = _currentEnvironmentName, at = _hiddenAt });
        PublishTelemetry();
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
        PublishTelemetry();
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
        AbortRecoveryOperation();

        _reconnectAttempts = 0;
        _softResyncAttempts = 0;
        _hardRefreshAttempts = 0;
        _recentGapCount = 0;
        _recoveryState = RecoveryState.Connecting;
        _lastRecoveryStartedAt = null;
        _lastSuccessfulRecoveryAt = null;
        _lastHardRefreshAt = null;
        _hiddenAt = null;
        _backgroundDuration = null;
        _isInBackground = false;
        _transportHealth = HealthStatus.Unknown;
        _sessionHealth = HealthStatus.Unknown;
        _streamHealth = HealthStatus.Unknown;
        _hostedUiHealth = HealthStatus.Unknown;
        _lastEventSeq = null;
        _lastStateVersion = null;
        _lastEventAt = null;
        _lastHeartbeatAt = null;
        _lastTransportActivityAt = null;
        _degradationReason = null;
        _lastRecoveryReason = null;

        _logger.Info("recovery.reset");
        RecoveryStateChanged?.Invoke(_recoveryState);
        PublishTelemetry();
    }
}
