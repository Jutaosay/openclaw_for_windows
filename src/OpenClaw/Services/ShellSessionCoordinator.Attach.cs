// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public sealed partial class ShellSessionCoordinator
{
    /// <summary>
    /// Attaches the coordinator to the required services.
    /// </summary>
    public Task AttachAsync(
        IShellSessionWebView webViewService,
        IShellSessionBridge bridge,
        RecoveryPolicyOptions? recoveryOptions = null,
        HeartbeatOptions? heartbeatOptions = null,
        IAppLogger? logger = null)
    {
        DetachServiceSubscriptions();

        _webViewService = webViewService;
        _bridge = bridge;
        _recoveryOptions = recoveryOptions ?? _recoveryOptions;
        _heartbeatOptions = heartbeatOptions ?? _heartbeatOptions;
        _logger = logger ?? _logger;

        AttachServiceSubscriptions();
        _logger.Info("ShellSessionCoordinator attached.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up coordinator resources.
    /// </summary>
    public void Dispose()
    {
        DetachServiceSubscriptions();
        AbortRecoveryOperation();
    }

    private void AttachServiceSubscriptions()
    {
        if (_webViewService is not null)
        {
            _webViewService.ConnectionStateChanged += OnConnectionStateChanged;
            _webViewService.NavigationErrorOccurred += OnNavigationError;
            _webViewService.NavigationCompleted += OnNavigationCompleted;
            _webViewService.ControlUiSnapshotUpdated += OnHostedUiStateUpdated;
            _webViewService.HeartbeatObserved += OnHeartbeatObserved;
            _webViewService.HeartbeatFailed += OnHeartbeatFailed;
        }

        if (_bridge is not null)
        {
            _bridge.SessionReady += OnSessionReady;
            _bridge.EventGapDetected += OnEventGapDetected;
        }
    }

    private void DetachServiceSubscriptions()
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
    }
}
