// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public sealed partial class ShellSessionCoordinator
{
    private async Task<GapRecoveryAction> GetPreferredGapRecoveryAsync()
    {
        var webViewService = _webViewService;
        if (webViewService is null)
        {
            return GapRecoveryAction.Reconnect;
        }

        try
        {
            var snapshot = await webViewService.InspectControlUiStateAsync();
            if (TryApplyAuthIssueFromSnapshot(snapshot))
            {
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
        var webViewService = _webViewService;
        if (webViewService is null)
        {
            return true;
        }

        try
        {
            var snapshot = await webViewService.InspectControlUiStateAsync();
            if (snapshot.Phase == ControlUiPhase.Unavailable)
            {
                return true;
            }

            if (TryApplyAuthIssueFromSnapshot(snapshot))
            {
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

    private bool TryResolveReloadFallback(ControlUiProbeSnapshot snapshot, string reason, string stage)
    {
        if (TryHandleReloadBlockedByAuth(snapshot, reason, stage))
        {
            return true;
        }

        if (TryHandleReloadDeferredForInput(snapshot, reason, stage))
        {
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

        ApplySuccessfulReloadFallback(snapshot);
        return true;
    }

    private bool TryApplyAuthIssueFromSnapshot(ControlUiProbeSnapshot snapshot)
    {
        if (snapshot.Phase is not (ControlUiPhase.AuthRequired or ControlUiPhase.PairingRequired or ControlUiPhase.OriginRejected))
        {
            return false;
        }

        MarkRecoveryAuthIssue(snapshot.DetailOrSummary);
        return true;
    }

    private bool TryHandleReloadBlockedByAuth(ControlUiProbeSnapshot snapshot, string reason, string stage)
    {
        if (!TryApplyAuthIssueFromSnapshot(snapshot))
        {
            return false;
        }

        _logger.Warning("recovery.reload.skipped", new
        {
            reason,
            stage,
            phase = snapshot.Phase,
            detail = snapshot.DetailOrSummary
        });
        return true;
    }

    private bool TryHandleReloadDeferredForInput(ControlUiProbeSnapshot snapshot, string reason, string stage)
    {
        if (!snapshot.InputFocused)
        {
            return false;
        }

        const string deferredReason = "Automatic reload deferred while a text input is focused.";
        if (snapshot.Phase == ControlUiPhase.Connected)
        {
            MarkRecoveryReadyWithoutRecordingSuccess(deferredReason);
        }
        else
        {
            MarkRecoveryDegraded(deferredReason);
        }
        _logger.Info("recovery.reload.deferred", new
        {
            reason,
            stage,
            phase = snapshot.Phase,
            inputFocused = true
        });
        return true;
    }

    private void ApplySuccessfulReloadFallback(ControlUiProbeSnapshot snapshot)
    {
        if (snapshot.Phase == ControlUiPhase.Connected)
        {
            MarkRecoveryReady();
            ResetEscalationCounters();
        }
        else
        {
            MarkRecoveryConnecting();
        }
    }
}
