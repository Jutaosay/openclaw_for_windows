// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public interface IShellSessionWebView
{
    event Action<ConnectionState>? ConnectionStateChanged;
    event Action<string>? NavigationErrorOccurred;
    event Action<string?>? NavigationCompleted;
    event Action<HeartbeatProbeResult>? HeartbeatObserved;
    event Action<string>? HeartbeatFailed;
    event Action<ControlUiProbeSnapshot>? ControlUiSnapshotUpdated;

    Task<ControlUiProbeSnapshot> InspectControlUiStateAsync();
    void Reload();
    int TotalControlUiInspectionRequests { get; }
    int CachedControlUiInspectionRequests { get; }
    int CoalescedControlUiInspectionRequests { get; }
    int HeartbeatRecoveryRequests { get; }
}

public interface IShellSessionBridge
{
    event Action<SessionReadyEventArgs>? SessionReady;
    event Action<EventGapEventArgs>? EventGapDetected;

    Task<bool> RequestSessionRefreshAsync();
    Task<bool> RequestRecentMessagesAsync();
    Task<bool> RequestLightweightSyncAsync();
    Task<bool> NotifyReconnectIntentAsync();
}

internal sealed class ShellSessionWebViewAdapter : IShellSessionWebView
{
    private readonly WebViewService _inner;

    public ShellSessionWebViewAdapter(WebViewService inner)
    {
        _inner = inner;
    }

    public event Action<ConnectionState>? ConnectionStateChanged
    {
        add => _inner.ConnectionStateChanged += value;
        remove => _inner.ConnectionStateChanged -= value;
    }

    public event Action<string>? NavigationErrorOccurred
    {
        add => _inner.NavigationErrorOccurred += value;
        remove => _inner.NavigationErrorOccurred -= value;
    }

    public event Action<string?>? NavigationCompleted
    {
        add => _inner.NavigationCompleted += value;
        remove => _inner.NavigationCompleted -= value;
    }

    public event Action<HeartbeatProbeResult>? HeartbeatObserved
    {
        add => _inner.HeartbeatObserved += value;
        remove => _inner.HeartbeatObserved -= value;
    }

    public event Action<string>? HeartbeatFailed
    {
        add => _inner.HeartbeatFailed += value;
        remove => _inner.HeartbeatFailed -= value;
    }

    public event Action<ControlUiProbeSnapshot>? ControlUiSnapshotUpdated
    {
        add => _inner.ControlUiSnapshotUpdated += value;
        remove => _inner.ControlUiSnapshotUpdated -= value;
    }

    public Task<ControlUiProbeSnapshot> InspectControlUiStateAsync() => _inner.InspectControlUiStateAsync();

    public void Reload() => _inner.Reload();

    public int TotalControlUiInspectionRequests => _inner.TotalControlUiInspectionRequests;

    public int CachedControlUiInspectionRequests => _inner.CachedControlUiInspectionRequests;

    public int CoalescedControlUiInspectionRequests => _inner.CoalescedControlUiInspectionRequests;

    public int HeartbeatRecoveryRequests => _inner.HeartbeatRecoveryRequests;
}

internal sealed class ShellSessionBridgeAdapter : IShellSessionBridge
{
    private readonly HostedUiBridge _inner;

    public ShellSessionBridgeAdapter(HostedUiBridge inner)
    {
        _inner = inner;
    }

    public event Action<SessionReadyEventArgs>? SessionReady
    {
        add => _inner.SessionReady += value;
        remove => _inner.SessionReady -= value;
    }

    public event Action<EventGapEventArgs>? EventGapDetected
    {
        add => _inner.EventGapDetected += value;
        remove => _inner.EventGapDetected -= value;
    }

    public Task<bool> RequestSessionRefreshAsync() => _inner.RequestSessionRefreshAsync();

    public Task<bool> RequestRecentMessagesAsync() => _inner.RequestRecentMessagesAsync();

    public Task<bool> RequestLightweightSyncAsync() => _inner.RequestLightweightSyncAsync();

    public Task<bool> NotifyReconnectIntentAsync() => _inner.NotifyReconnectIntentAsync();
}
