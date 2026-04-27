// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Services;

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    private static HeartbeatIndicatorViewModel CreateRunIndicator(int index)
    {
        return new HeartbeatIndicatorViewModel
        {
            FillBrush = CreateRunIndicatorBrush(RunIndicatorMode.Wait),
            FillOpacity = CreateRunIndicatorOpacity(RunIndicatorMode.Wait, index, 0),
        };
    }

    public void AdvanceRunIndicators()
    {
        if (_runIndicatorMode != RunIndicatorMode.Live)
        {
            return;
        }

        _runAnimationFrame = (_runAnimationFrame + 1) % RunIndicatorCount;
        ApplyRunIndicators();
    }

    private void SetRunIndicatorMode(RunIndicatorMode mode)
    {
        if (_runIndicatorMode == mode)
        {
            if (mode != RunIndicatorMode.Live)
            {
                ApplyRunIndicators();
            }

            return;
        }

        _runIndicatorMode = mode;
        _runAnimationFrame = 0;
        IsRunIndicatorsAnimating = mode == RunIndicatorMode.Live;
        ApplyRunIndicators();
    }

    private void ApplyRunIndicators()
    {
        for (var index = 0; index < RunIndicators.Count; index++)
        {
            RunIndicators[index].FillBrush = CreateRunIndicatorBrush(_runIndicatorMode);
            RunIndicators[index].FillOpacity = CreateRunIndicatorOpacity(_runIndicatorMode, index, _runAnimationFrame);
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush CreateRunIndicatorBrush(RunIndicatorMode mode)
    {
        return mode switch
        {
            RunIndicatorMode.Live => SuccessBrush,
            _ => WarningBrush,
        };
    }

    private static double CreateRunIndicatorOpacity(RunIndicatorMode mode, int index, int frame)
    {
        if (mode != RunIndicatorMode.Live)
        {
            var normalized = (index + 1d) / RunIndicatorCount;
            return 0.24 + (normalized * 0.7);
        }

        var distance = (index - frame + RunIndicatorCount) % RunIndicatorCount;
        var factor = 1d - (distance / (double)RunIndicatorCount);
        return 0.18 + (factor * 0.78);
    }
}
