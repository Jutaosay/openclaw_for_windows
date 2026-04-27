// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public sealed partial class ShellSessionCoordinator
{
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
        if (string.IsNullOrEmpty(_currentGatewayUrl))
        {
            return false;
        }

        return _currentGatewayUrl.Contains("cloudflare") ||
               _currentGatewayUrl.Contains("workers.dev") ||
               _currentGatewayUrl.Contains("trycloudflare.com");
    }
}
