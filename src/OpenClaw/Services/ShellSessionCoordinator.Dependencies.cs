// Copyright (c) Lanstack @openclaw. All rights reserved.

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
