// Copyright (c) Lanstack @openclaw. All rights reserved.

namespace OpenClaw.Services;

public enum ConnectionState
{
    Offline,
    Loading,
    GatewayConnecting,
    Connected,
    Reconnecting,
    AuthFailed,
    Error,
}

public enum HeartbeatProbeStatus
{
    Healthy,
    SessionBlocked,
    Connecting,
    Failure,
}

public sealed record HeartbeatProbeResult(HeartbeatProbeStatus Status, string Message)
{
    public static HeartbeatProbeResult Healthy(string message) => new(HeartbeatProbeStatus.Healthy, message);
    public static HeartbeatProbeResult SessionBlocked(string message) => new(HeartbeatProbeStatus.SessionBlocked, message);
    public static HeartbeatProbeResult Connecting(string message) => new(HeartbeatProbeStatus.Connecting, message);
    public static HeartbeatProbeResult Failure(string message) => new(HeartbeatProbeStatus.Failure, message);
}

public enum ControlUiPhase
{
    Unknown,
    Loading,
    PageLoaded,
    GatewayConnecting,
    Connected,
    AuthRequired,
    PairingRequired,
    OriginRejected,
    GatewayError,
    Unavailable,
}

public sealed record ControlUiProbeSnapshot(
    ControlUiPhase Phase,
    string Summary,
    string Detail,
    string Url,
    bool ShellDetected,
    bool IsBusy,
    bool InputFocused,
    string WorkState,
    string CurrentModel)
{
    public static ControlUiProbeSnapshot Unknown { get; } =
        new(ControlUiPhase.Unknown, string.Empty, string.Empty, string.Empty, false, false, false, string.Empty, string.Empty);

    public bool IsIssue => Phase is ControlUiPhase.AuthRequired
        or ControlUiPhase.PairingRequired
        or ControlUiPhase.OriginRejected
        or ControlUiPhase.GatewayError;

    public bool IsTerminal => Phase is ControlUiPhase.Connected
        or ControlUiPhase.AuthRequired
        or ControlUiPhase.PairingRequired
        or ControlUiPhase.OriginRejected
        or ControlUiPhase.GatewayError;

    public string DetailOrSummary => string.IsNullOrWhiteSpace(Detail) ? Summary : Detail;

    public string IssueKey => $"{Phase}:{DetailOrSummary}:{Url}";

    public static ControlUiProbeSnapshot Loading(string? url) =>
        new(ControlUiPhase.Loading, "Page is loading.", string.Empty, url ?? string.Empty, false, false, false, "loading", string.Empty);

    public static ControlUiProbeSnapshot PageLoaded(string? url) =>
        new(ControlUiPhase.PageLoaded, "Gateway UI loaded.", "Waiting for the hosted Control UI to report its Gateway session state.", url ?? string.Empty, false, false, false, "idle", string.Empty);

    public static ControlUiProbeSnapshot Unavailable(string detail) =>
        new(ControlUiPhase.Unavailable, "Control UI state is unavailable.", detail, string.Empty, false, false, false, string.Empty, string.Empty);
}

public record SessionReadyEventArgs(
    string DetectedAt,
    string Model,
    string Uri
);

public record EventGapEventArgs(
    long ExpectedSeq,
    long GotSeq,
    string? LastStateVersion,
    string? CurrentStateVersion,
    string DetectedAt
);
