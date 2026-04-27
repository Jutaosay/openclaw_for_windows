// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml.Media;
using OpenClaw.Helpers;
using OpenClaw.Services;

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    private static (string Text, Brush Brush) FormatHeartbeatSummary(HeartbeatProbeStatus status)
    {
        return status switch
        {
            HeartbeatProbeStatus.Healthy => (StringResources.HeartbeatOk, SuccessBrush),
            HeartbeatProbeStatus.Connecting => (StringResources.HeartbeatWait, WarningBrush),
            HeartbeatProbeStatus.SessionBlocked => (StringResources.HeartbeatBlocked, WarningBrush),
            HeartbeatProbeStatus.Failure => (StringResources.HeartbeatFailed, WarningBrush),
            _ => (DefaultHeartbeatSummary, WarningBrush),
        };
    }

    private static (string Text, Brush Brush) FormatLatencySummary(ControlUiLatencySnapshot snapshot)
    {
        if (snapshot.IsSuccess && snapshot.RoundtripTimeMs is long roundtripTimeMs)
        {
            var brush = roundtripTimeMs switch
            {
                <= 200 => SuccessBrush,
                <= 500 => WarningBrush,
                _ => ErrorBrush,
            };

            return ($"{roundtripTimeMs} ms", brush);
        }

        if (snapshot.State == ControlUiLatencyState.Stale && snapshot.RoundtripTimeMs is long staleRoundtripTimeMs)
        {
            var brush = staleRoundtripTimeMs switch
            {
                <= 200 => SuccessBrush,
                <= 500 => WarningBrush,
                _ => ErrorBrush,
            };

            return ($"{staleRoundtripTimeMs} ms", brush);
        }

        return (DefaultLatencySummary, NeutralBrush);
    }

    private static string FormatModelSummary(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return DefaultModelSummary;
        }

        return model.Trim();
    }

    private static (string Text, Brush Brush) FormatAccessSummary(ControlUiProbeSnapshot snapshot)
    {
        return snapshot.Phase switch
        {
            ControlUiPhase.Connected => ("AUTH OK", SuccessBrush),
            ControlUiPhase.AuthRequired => ("AUTH LOGIN", WarningBrush),
            ControlUiPhase.PairingRequired => ("AUTH PAIR", WarningBrush),
            ControlUiPhase.OriginRejected => ("AUTH ORIGIN", WarningBrush),
            ControlUiPhase.GatewayConnecting or ControlUiPhase.PageLoaded or ControlUiPhase.Loading => ("AUTH WAIT", WarningBrush),
            _ => (DefaultAccessSummary, WarningBrush),
        };
    }

    private static (string Text, Brush Brush, RunIndicatorMode Mode) FormatWorkStatus(ControlUiProbeSnapshot snapshot)
    {
        if (snapshot.IsBusy || string.Equals(snapshot.WorkState, "busy", StringComparison.OrdinalIgnoreCase))
        {
            return ("LIVE", SuccessBrush, RunIndicatorMode.Live);
        }

        if (string.Equals(snapshot.WorkState, "idle", StringComparison.OrdinalIgnoreCase) ||
            snapshot.Phase == ControlUiPhase.Connected)
        {
            return ("IDLE", WarningBrush, RunIndicatorMode.Idle);
        }

        return (DefaultWorkStatus, WarningBrush, RunIndicatorMode.Wait);
    }
}
