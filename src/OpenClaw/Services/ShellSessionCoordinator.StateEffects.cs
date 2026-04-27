// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public sealed partial class ShellSessionCoordinator
{
    private void UpdateSnapshotActivity()
    {
        _lastTransportActivityAt = DateTimeOffset.Now;
        _lastEventAt = _lastTransportActivityAt;
    }

    private void ApplySnapshotHealth(ControlUiProbeSnapshot snapshot)
    {
        _hostedUiHealth = MapHostedUiHealth(snapshot);
        _sessionHealth = MapSessionHealth(snapshot);
        _streamHealth = MapStreamHealth(snapshot);
    }

    private void LogHostedUiState(ControlUiProbeSnapshot snapshot)
    {
        _logger.Info("hosted_ui.state", new
        {
            phase = snapshot.Phase,
            summary = snapshot.Summary,
            shellDetected = snapshot.ShellDetected
        });
    }

    private void ApplyHostedUiRecoveryState(ControlUiProbeSnapshot snapshot)
    {
        switch (snapshot.Phase)
        {
            case ControlUiPhase.Connected when _recoveryState is RecoveryState.Connecting or RecoveryState.Reconnecting or RecoveryState.Resyncing:
                MarkRecoveryReady();
                ResetEscalationCounters();
                break;
            case ControlUiPhase.AuthRequired:
            case ControlUiPhase.PairingRequired:
            case ControlUiPhase.OriginRejected:
                MarkRecoveryAuthIssue(snapshot.DetailOrSummary);
                break;
        }
    }

    private void LogIgnoredGap(EventGapEventArgs args)
    {
        _logger.Info("stream.gap.ignored", new
        {
            reason = "background",
            expectedSeq = args.ExpectedSeq,
            gotSeq = args.GotSeq
        });
    }

    private void ApplyDetectedGap(EventGapEventArgs args)
    {
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
    }
}
