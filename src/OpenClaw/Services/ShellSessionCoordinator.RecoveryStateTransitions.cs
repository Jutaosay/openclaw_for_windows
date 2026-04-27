// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public sealed partial class ShellSessionCoordinator
{
    private static bool IsSessionAlive(ControlUiProbeSnapshot snapshot)
    {
        return snapshot.Phase is ControlUiPhase.Connected
            or ControlUiPhase.GatewayConnecting
            or ControlUiPhase.PageLoaded;
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

    private void MarkRecoveryReady()
    {
        _degradationReason = null;
        _lastSuccessfulRecoveryAt = DateTimeOffset.Now;
        SetRecoveryState(RecoveryState.Ready);
    }

    private void MarkRecoveryReadyWithoutRecordingSuccess(string? reason = null)
    {
        _degradationReason = reason;
        SetRecoveryState(RecoveryState.Ready);
    }

    private void MarkRecoveryHealthy()
    {
        _degradationReason = null;
    }

    private void MarkRecoveryConnecting()
    {
        SetRecoveryState(RecoveryState.Connecting);
    }

    private void MarkRecoveryAuthIssue(string? reason)
    {
        _degradationReason = reason;
        SetRecoveryState(RecoveryState.AuthIssue);
    }

    private void MarkRecoveryDegraded(string? reason = null)
    {
        if (!string.IsNullOrWhiteSpace(reason))
        {
            _degradationReason = reason;
        }

        SetRecoveryState(RecoveryState.Degraded);
    }

    private void MarkRecoveryFailed(string? reason = null)
    {
        if (!string.IsNullOrWhiteSpace(reason))
        {
            _degradationReason = reason;
        }

        SetRecoveryState(RecoveryState.Failed);
    }
}
