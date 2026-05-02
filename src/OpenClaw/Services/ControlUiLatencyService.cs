// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Diagnostics;

namespace OpenClaw.Services;

/// <summary>
/// Periodically pings the active Control UI host and publishes round-trip latency updates.
/// </summary>
public sealed class ControlUiLatencyService : IDisposable
{
    private const int ProbeIntervalSeconds = 3;

    private readonly HttpClient _probeHttpClient;
    private readonly TimeSpan _probeInterval;
    private readonly bool _disposeHttpClient;
    private PeriodicTimer? _probeTimer;
    private CancellationTokenSource? _probeCts;
    private Task? _probeTask;
    private string? _currentHost;
    private string? _currentUrl;
    private ControlUiLatencySnapshot _lastSuccessSnapshot = ControlUiLatencySnapshot.Unknown;
    private ControlUiLatencySnapshot _lastPublishedSnapshot = ControlUiLatencySnapshot.Unknown;

    public ControlUiLatencyService()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(5) }, TimeSpan.FromSeconds(ProbeIntervalSeconds), disposeHttpClient: true)
    {
    }

    public ControlUiLatencyService(HttpMessageHandler messageHandler, TimeSpan? probeInterval = null)
        : this(new HttpClient(messageHandler) { Timeout = TimeSpan.FromSeconds(5) }, probeInterval ?? TimeSpan.FromSeconds(ProbeIntervalSeconds), disposeHttpClient: true)
    {
    }

    private ControlUiLatencyService(HttpClient probeHttpClient, TimeSpan probeInterval, bool disposeHttpClient)
    {
        ArgumentNullException.ThrowIfNull(probeHttpClient);
        _probeHttpClient = probeHttpClient;
        _probeInterval = probeInterval;
        _disposeHttpClient = disposeHttpClient;
    }

    /// <summary>
    /// Raised whenever a new latency snapshot is available.
    /// </summary>
    public event Action<ControlUiLatencySnapshot>? LatencyUpdated;

    /// <summary>
    /// Starts probing the supplied Control UI URL.
    /// </summary>
    public void Start(string? controlUiUrl)
    {
        var probeUri = TryGetProbeUri(controlUiUrl);
        var host = TryGetProbeHost(probeUri);
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

        if (probeUri is null || string.IsNullOrWhiteSpace(host))
        {
            PublishIfChanged(ControlUiLatencySnapshot.Unknown);
            return;
        }

        _probeCts = new CancellationTokenSource();
        _probeTimer = new PeriodicTimer(_probeInterval);
        _probeTask = RunProbeLoopAsync(probeUri, host, _probeCts.Token);
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
        if (_disposeHttpClient)
        {
            _probeHttpClient.Dispose();
        }
    }

    private async Task RunProbeLoopAsync(Uri probeUri, string host, CancellationToken cancellationToken)
    {
        try
        {
            await PublishLatencyAsync(probeUri, host, cancellationToken).ConfigureAwait(false);

            var timer = _probeTimer;
            if (timer is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await PublishLatencyAsync(probeUri, host, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task PublishLatencyAsync(Uri probeUri, string host, CancellationToken cancellationToken)
    {
        var snapshot = await ProbeAsync(probeUri, host, cancellationToken).ConfigureAwait(false);
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

    private async Task<ControlUiLatencySnapshot> ProbeAsync(Uri probeUri, string host, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, probeUri);
            request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache, no-store, max-age=0");
            request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
            request.Headers.TryAddWithoutValidation("Accept", "application/json,text/plain,*/*");

            var stopwatch = Stopwatch.StartNew();
            using var response = await _probeHttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var proxyHint = response.Headers.TryGetValues("cf-ray", out _) ? " via Cloudflare" : string.Empty;
            return ControlUiLatencySnapshot.Success(
                host,
                stopwatch.ElapsedMilliseconds,
                $"HTTP {(int)response.StatusCode}{proxyHint}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ControlUiLatencySnapshot.Failure(host, ex.Message);
        }
    }

    private static Uri? TryGetProbeUri(string? controlUiUrl)
    {
        if (string.IsNullOrWhiteSpace(controlUiUrl) ||
            !Uri.TryCreate(controlUiUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme is "http" or "https"
            ? CreateControlUiConfigUri(uri)
            : null;
    }

    private static Uri CreateControlUiConfigUri(Uri controlUiUri)
    {
        var builder = new UriBuilder(controlUiUri)
        {
            Query = string.Empty,
            Fragment = string.Empty,
        };

        var basePath = builder.Path;
        if (string.IsNullOrWhiteSpace(basePath) || basePath == "/")
        {
            basePath = "/";
        }
        else if (!basePath.EndsWith('/'))
        {
            basePath += "/";
        }

        builder.Path = $"{basePath}__openclaw/control-ui-config.json";
        return builder.Uri;
    }

    private static string? TryGetProbeHost(Uri? uri)
    {
        if (uri is null)
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

    public static ControlUiLatencySnapshot Success(string host, long roundtripTimeMs, string? detail = null) =>
        new(ControlUiLatencyState.Success, host, roundtripTimeMs, detail);

    public static ControlUiLatencySnapshot Failure(string host, string? detail = null) =>
        new(ControlUiLatencyState.Failure, host, null, detail);

    public bool IsSuccess => State == ControlUiLatencyState.Success && RoundtripTimeMs is not null;
}
