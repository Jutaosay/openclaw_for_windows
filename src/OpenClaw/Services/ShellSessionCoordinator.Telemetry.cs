// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public sealed partial class ShellSessionCoordinator
{
    private void RefreshInstrumentationCounters()
    {
        if (_webViewService is not null)
        {
            _totalControlUiInspectionRequests = _webViewService.TotalControlUiInspectionRequests;
            _cachedControlUiInspectionRequests = _webViewService.CachedControlUiInspectionRequests;
            _coalescedControlUiInspectionRequests = _webViewService.CoalescedControlUiInspectionRequests;
            _heartbeatRecoveryRequests = _webViewService.HeartbeatRecoveryRequests;
        }

        _deferredSaveRequests = AppTelemetry.DeferredSaveRequests;
        _deferredSaveCoalescedRequests = AppTelemetry.DeferredSaveCoalescedRequests;
    }

    /// <summary>
    /// Gets the latest telemetry snapshot.
    /// </summary>
    public RecoveryTelemetrySnapshot GetTelemetrySnapshot()
    {
        RefreshInstrumentationCounters();

        return new RecoveryTelemetrySnapshot(
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
            _heartbeatOptions.IntervalSeconds,
            _totalWebViewRecreations,
            _mergedWebViewRecreationRequests,
            _totalControlUiInspectionRequests,
            _cachedControlUiInspectionRequests,
            _coalescedControlUiInspectionRequests,
            _deferredSaveRequests,
            _deferredSaveCoalescedRequests,
            _heartbeatRecoveryRequests,
            _lastInstrumentationEvent
        );
    }

    private void PublishTelemetry()
    {
        TelemetryUpdated?.Invoke(GetTelemetrySnapshot());
    }
}
