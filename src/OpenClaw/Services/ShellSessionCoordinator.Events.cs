// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public sealed partial class ShellSessionCoordinator
{
    /// <summary>
    /// Called when WebView reports navigation completed.
    /// </summary>
    private void OnNavigationCompleted(string? uri) => HandleNavigationCompleted(uri);

    /// <summary>
    /// Called when hosted UI reports ready state.
    /// </summary>
    private void OnHostedUiStateUpdated(ControlUiProbeSnapshot snapshot) => HandleHostedUiStateUpdated(snapshot);

    /// <summary>
    /// Called when hosted UI reports session ready.
    /// </summary>
    private void OnSessionReady(SessionReadyEventArgs args) => HandleSessionReady(args);

    /// <summary>
    /// Called when an event gap is detected.
    /// </summary>
    private async void OnEventGapDetected(EventGapEventArgs args) => await HandleEventGapDetectedAsync(args);

    /// <summary>
    /// Called when connection state changes.
    /// </summary>
    private void OnConnectionStateChanged(ConnectionState state) => HandleConnectionStateChanged(state);

    /// <summary>
    /// Called when a navigation error occurs.
    /// </summary>
    private void OnNavigationError(string message) => HandleNavigationError(message);

    /// <summary>
    /// Updates heartbeat observation.
    /// </summary>
    private void OnHeartbeatObserved(HeartbeatProbeResult result) => HandleHeartbeatObserved(result);

    private async void OnHeartbeatFailed(string message) => await HandleHeartbeatFailedAsync(message);
}
