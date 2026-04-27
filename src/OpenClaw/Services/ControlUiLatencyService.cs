// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Net.NetworkInformation;

namespace OpenClaw.Services;

/// <summary>
/// Periodically pings the active Control UI host and publishes round-trip latency updates.
/// </summary>
public sealed class ControlUiLatencyService : IDisposable
{
    private const int ProbeIntervalSeconds = 3;
    private const int PingTimeoutMilliseconds = 2000;

    private PeriodicTimer? _probeTimer;
    private CancellationTokenSource? _probeCts;
    private string? _currentHost;
    private string? _currentUrl;
    private ControlUiLatencySnapshot _lastSuccessSnapshot = ControlUiLatencySnapshot.Unknown;
    private ControlUiLatencySnapshot _lastPublishedSnapshot = ControlUiLatencySnapshot.Unknown;

    /// <summary>
    /// Raised whenever a new latency snapshot is available.
    /// </summary>
    public event Action<ControlUiLatencySnapshot>? LatencyUpdated;

    /// <summary>
    /// Starts probing the supplied Control UI URL.
    /// </summary>
    public void Start(string? controlUiUrl)
    {
        var host = TryGetPingHost(controlUiUrl);
        if (_probeCts is not null &&
            !_probeCts.IsCancellationRequested &&
            string.Equals(_currentUrl, controlUiUrl, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_currentHost, host, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Stop();

        _currentUrl = controlUiUrl;
        _currentHost = host;
        _lastSuccessSnapshot = ControlUiLatencySnapshot.Unknown;
        _lastPublishedSnapshot = ControlUiLatencySnapshot.Unknown;

        if (string.IsNullOrWhiteSpace(host))
        {
            PublishIfChanged(ControlUiLatencySnapshot.Unknown);
            return;
        }

        _probeCts = new CancellationTokenSource();
        _probeTimer = new PeriodicTimer(TimeSpan.FromSeconds(ProbeIntervalSeconds));
        _ = RunProbeLoopAsync(host, _probeCts.Token);
    }

    /// <summary>
    /// Stops probing and releases timers.
    /// </summary>
    public void Stop()
    {
        _currentUrl = null;
        _currentHost = null;
        _lastSuccessSnapshot = ControlUiLatencySnapshot.Unknown;
        _lastPublishedSnapshot = ControlUiLatencySnapshot.Unknown;

        if (_probeCts is not null)
        {
            _probeCts.Cancel();
            _probeCts.Dispose();
            _probeCts = null;
        }

        _probeTimer?.Dispose();
        _probeTimer = null;
    }

    public void Dispose()
    {
        Stop();
    }

    private async Task RunProbeLoopAsync(string host, CancellationToken cancellationToken)
    {
        await PublishLatencyAsync(host, cancellationToken).ConfigureAwait(false);

        var timer = _probeTimer;
        if (timer is null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await PublishLatencyAsync(host, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task PublishLatencyAsync(string host, CancellationToken cancellationToken)
    {
        var snapshot = await ProbeAsync(host).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (snapshot.IsSuccess)
        {
            _lastSuccessSnapshot = snapshot;
            PublishIfChanged(snapshot);
            return;
        }

        if (_lastSuccessSnapshot.IsSuccess)
        {
            PublishIfChanged(_lastSuccessSnapshot with
            {
                State = ControlUiLatencyState.Stale,
                Detail = snapshot.Detail,
            });
            return;
        }

        PublishIfChanged(snapshot.State == ControlUiLatencyState.Unknown
            ? snapshot
            : ControlUiLatencySnapshot.Unknown with { Detail = snapshot.Detail });
    }

    private void PublishIfChanged(ControlUiLatencySnapshot snapshot)
    {
        if (_lastPublishedSnapshot.Equals(snapshot))
        {
            return;
        }

        _lastPublishedSnapshot = snapshot;
        LatencyUpdated?.Invoke(snapshot);
    }

    private static async Task<ControlUiLatencySnapshot> ProbeAsync(string host)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, PingTimeoutMilliseconds).ConfigureAwait(false);

            return reply.Status == IPStatus.Success
                ? ControlUiLatencySnapshot.Success(host, reply.RoundtripTime)
                : ControlUiLatencySnapshot.Failure(host, reply.Status.ToString());
        }
        catch (PingException ex)
        {
            return ControlUiLatencySnapshot.Failure(host, ex.InnerException?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            return ControlUiLatencySnapshot.Failure(host, ex.Message);
        }
    }

    private static string? TryGetPingHost(string? controlUiUrl)
    {
        if (string.IsNullOrWhiteSpace(controlUiUrl) ||
            !Uri.TryCreate(controlUiUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = string.IsNullOrWhiteSpace(uri.IdnHost)
            ? uri.Host
            : uri.IdnHost;

        return string.IsNullOrWhiteSpace(host)
            ? null
            : host.Trim('[', ']');
    }
}

public enum ControlUiLatencyState
{
    Unknown,
    Success,
    Stale,
    Failure,
}

/// <summary>
/// Represents the latest latency probe result for the Control UI host.
/// </summary>
public readonly record struct ControlUiLatencySnapshot(
    ControlUiLatencyState State,
    string Host,
    long? RoundtripTimeMs,
    string? Detail = null)
{
    public static ControlUiLatencySnapshot Unknown => new(ControlUiLatencyState.Unknown, string.Empty, null);

    public static ControlUiLatencySnapshot Success(string host, long roundtripTimeMs) =>
        new(ControlUiLatencyState.Success, host, roundtripTimeMs);

    public static ControlUiLatencySnapshot Failure(string host, string? detail = null) =>
        new(ControlUiLatencyState.Failure, host, null, detail);

    public bool IsSuccess => State == ControlUiLatencyState.Success && RoundtripTimeMs is not null;
}
