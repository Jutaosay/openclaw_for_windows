// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public sealed partial class ShellSessionCoordinator
{
    /// <summary>
    /// Attaches the coordinator to the required services.
    /// </summary>
    public Task AttachAsync(
        WebViewService webViewService,
        HostedUiBridge bridge,
        RecoveryPolicyOptions? recoveryOptions = null,
        HeartbeatOptions? heartbeatOptions = null)
    {
        return AttachAsync(
            new ShellSessionWebViewAdapter(webViewService),
            new ShellSessionBridgeAdapter(bridge),
            recoveryOptions,
            heartbeatOptions);
    }

    internal Task AttachAsync(
        IShellSessionWebView webViewService,
        IShellSessionBridge bridge,
        RecoveryPolicyOptions? recoveryOptions = null,
        HeartbeatOptions? heartbeatOptions = null)
    {
        DetachServiceSubscriptions();

        _webViewService = webViewService;
        _bridge = bridge;
        _recoveryOptions = recoveryOptions ?? App.Configuration.Settings.RecoveryPolicy;
        _heartbeatOptions = heartbeatOptions ?? App.Configuration.Settings.Heartbeat;
        _logger = App.Logger;

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
