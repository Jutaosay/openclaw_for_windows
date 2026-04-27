// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Helpers;
using OpenClaw.Services;

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    private static HeartbeatIndicatorViewModel CreateHeartbeatIndicator(int index)
    {
        return new HeartbeatIndicatorViewModel
        {
            FillBrush = CreateIndicatorBrush(HeartbeatProbeStatus.Connecting),
            FillOpacity = CreateIndicatorOpacity(index),
        };
    }

    private void EnsureHeartbeatUiPrimed()
    {
        if (HeartbeatSummary != DefaultHeartbeatSummary)
        {
            return;
        }

        HeartbeatSummary = StringResources.HeartbeatWait;
        HeartbeatSummaryBrush = WarningBrush;
    }

    private void StartHeartbeatForSelectedEnvironment()
    {
        if (_selectedEnvironment is null)
        {
            return;
        }

        var interval = App.Configuration.Settings.Heartbeat.EnableHeartbeat
            ? App.Configuration.Settings.Heartbeat.IntervalSeconds
            : 0;
        _webViewService.StartHeartbeat(_selectedEnvironment.GatewayUrl, interval);
    }

    private void OnHeartbeatObserved(HeartbeatProbeResult result)
    {
        RunOnUiThread(() =>
        {
            (HeartbeatSummary, HeartbeatSummaryBrush) = FormatHeartbeatSummary(result.Status);
            UpdateHeartbeatIndicators(result.Status);
            UpdateStatusPresentation();
        });
    }

    private void UpdateHeartbeatIndicators(HeartbeatProbeStatus status)
    {
        if (HeartbeatIndicators.Count == 0)
        {
            return;
        }

        if (status != HeartbeatProbeStatus.Healthy)
        {
            ResetHeartbeatIndicatorsToWarning();
            _lastHeartbeatStatus = status;
            return;
        }

        if (_lastHeartbeatStatus != HeartbeatProbeStatus.Healthy)
        {
            ResetHeartbeatIndicatorsToWarning();
        }

        for (var index = 0; index < HeartbeatIndicators.Count - 1; index++)
        {
            HeartbeatIndicators[index].FillBrush = HeartbeatIndicators[index + 1].FillBrush;
            HeartbeatIndicators[index].FillOpacity = HeartbeatIndicators[index + 1].FillOpacity;
        }

        HeartbeatIndicators[^1].FillBrush = CreateIndicatorBrush(status);
        HeartbeatIndicators[^1].FillOpacity = 0.98;
        _lastHeartbeatStatus = status;
    }

    private void ResetHeartbeatIndicatorsToWarning()
    {
        for (var index = 0; index < HeartbeatIndicators.Count; index++)
        {
            HeartbeatIndicators[index].FillBrush = CreateIndicatorBrush(HeartbeatProbeStatus.Connecting);
            HeartbeatIndicators[index].FillOpacity = CreateIndicatorOpacity(index);
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush CreateIndicatorBrush(HeartbeatProbeStatus status)
    {
        return status switch
        {
            HeartbeatProbeStatus.Healthy => SuccessBrush,
            _ => WarningBrush,
        };
    }

    private static double CreateIndicatorOpacity(int index)
    {
        var normalized = (index + 1d) / HeartbeatIndicatorCount;
        return 0.2 + (normalized * 0.65);
    }
}
