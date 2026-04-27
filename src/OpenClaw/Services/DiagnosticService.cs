// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.Web.WebView2.Core;
using OpenClaw.Helpers;
namespace OpenClaw.Services;

/// <summary>
/// Performs startup diagnostics: WebView2 runtime detection, network probes,
/// and auth/session invalidation detection.
/// </summary>
public class DiagnosticService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private static readonly string[] LocalLoopbackHosts = ["127.0.0.1", "localhost", "::1"];

    /// <summary>
    /// Checks whether the WebView2 runtime is installed and available.
    /// </summary>
    public static DiagnosticResult CheckWebView2Runtime()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrEmpty(version))
            {
                return DiagnosticResult.Fail(
                    StringResources.DiagnosticWebViewRuntimeNotFound,
                    StringResources.DiagnosticWebViewRuntimeNotFoundDetail);
            }

            return DiagnosticResult.Pass($"{StringResources.DiagnosticWebView2RuntimeLabel} v{version}");
        }
        catch (Exception ex)
        {
            return DiagnosticResult.Fail(
                StringResources.DiagnosticWebViewRuntimeCheckFailed,
                string.Format(StringResources.DiagnosticWebViewRuntimeCheckFailedDetailFormat, ex.Message));
        }
    }

    /// <summary>
    /// Probes network connectivity to the given gateway URL.
    /// </summary>
    public static async Task<DiagnosticResult> ProbeNetworkAsync(string? gatewayUrl, ControlUiProbeSnapshot? snapshot = null)
    {
        if (string.IsNullOrEmpty(gatewayUrl))
        {
            return DiagnosticResult.Skip(StringResources.DiagnosticNoGatewayUrlConfigured);
        }

        var uri = Uri.TryCreate(gatewayUrl, UriKind.Absolute, out var parsedUri) ? parsedUri : null;
        if (uri is not null &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !IsLoopbackLike(uri))
        {
            return DiagnosticResult.Warn(
                StringResources.DiagnosticNonLocalHttp,
                StringResources.DiagnosticNonLocalHttpDetail);
        }

        if (snapshot is not null && snapshot.Phase is not ControlUiPhase.Unknown and not ControlUiPhase.Unavailable)
        {
            return snapshot.Phase switch
            {
                ControlUiPhase.Connected => DiagnosticResult.Pass(StringResources.DiagnosticControlUiReachableActive),
                ControlUiPhase.Loading => DiagnosticResult.Warn(
                    StringResources.DiagnosticControlUiLoading,
                    StringResources.DiagnosticControlUiLoadingDetail),
                ControlUiPhase.PageLoaded or ControlUiPhase.GatewayConnecting => DiagnosticResult.Warn(
                    StringResources.DiagnosticControlUiEstablishing,
                    snapshot.DetailOrSummary),
                ControlUiPhase.AuthRequired => DiagnosticResult.Warn(
                    StringResources.DiagnosticControlUiAuthRequired,
                    snapshot.DetailOrSummary),
                ControlUiPhase.PairingRequired => DiagnosticResult.Warn(
                    StringResources.DiagnosticControlUiPairingRequired,
                    string.Format(StringResources.DiagnosticControlUiPairingRequiredDetailFormat, snapshot.DetailOrSummary)),
                ControlUiPhase.OriginRejected => DiagnosticResult.Warn(
                    StringResources.DiagnosticControlUiOriginRejected,
                    string.Format(StringResources.DiagnosticControlUiOriginRejectedDetailFormat, snapshot.DetailOrSummary)),
                ControlUiPhase.GatewayError => DiagnosticResult.Warn(
                    StringResources.DiagnosticControlUiGatewayWsFailing,
                    snapshot.DetailOrSummary),
                _ => DiagnosticResult.Skip(StringResources.DiagnosticControlUiStateUnavailable)
            };
        }

        try
        {
            using var response = await SharedHttpClient.GetAsync(gatewayUrl, HttpCompletionOption.ResponseHeadersRead);
            var statusCode = (int)response.StatusCode;

            return statusCode switch
            {
                >= 200 and < 300 =>
                    DiagnosticResult.Warn(
                        string.Format(StringResources.DiagnosticHttpReachableFormat, statusCode),
                        StringResources.DiagnosticHttpReachableDetail),
                401 or 403 =>
                    DiagnosticResult.Warn(
                        string.Format(StringResources.DiagnosticAccessRejectedFormat, statusCode),
                        StringResources.DiagnosticAccessRejectedDetail),
                409 =>
                    DiagnosticResult.Warn(
                        StringResources.DiagnosticGatewayWaitingApproval,
                        StringResources.DiagnosticGatewayWaitingApprovalDetail),
                429 =>
                    DiagnosticResult.Warn(
                        StringResources.DiagnosticAuthRateLimited,
                        StringResources.DiagnosticAuthRateLimitedDetail),
                405 =>
                    DiagnosticResult.Warn(
                        StringResources.DiagnosticMethodRejected,
                        StringResources.DiagnosticMethodRejectedDetail),
                301 or 302 or 303 or 307 or 308 =>
                    DiagnosticResult.Warn(
                        string.Format(StringResources.DiagnosticRedirectedFormat, statusCode),
                        StringResources.DiagnosticRedirectedDetail),
                404 =>
                    DiagnosticResult.Warn(
                        StringResources.DiagnosticPathNotFound,
                        StringResources.DiagnosticPathNotFoundDetail),
                >= 500 =>
                    DiagnosticResult.Fail(
                        string.Format(StringResources.DiagnosticGatewayReturnedFormat, statusCode, response.ReasonPhrase),
                        StringResources.DiagnosticGatewayReturnedServerFailureDetail),
                _ =>
                    DiagnosticResult.Warn(
                        string.Format(StringResources.DiagnosticGatewayReturnedFormat, statusCode, response.ReasonPhrase),
                        StringResources.DiagnosticGatewayReturnedDetail)
            };
        }
        catch (TaskCanceledException)
        {
            return DiagnosticResult.Fail(StringResources.DiagnosticGatewayTimeout, StringResources.DiagnosticGatewayTimeoutDetail);
        }
        catch (HttpRequestException ex)
        {
            return DiagnosticResult.Fail(StringResources.DiagnosticGatewayUnreachable, ex.Message);
        }
        catch (Exception ex)
        {
            return DiagnosticResult.Fail(StringResources.DiagnosticNetworkProbeFailed, ex.Message);
        }
    }

    /// <summary>
    /// Checks if common session indicators are present in the WebView2.
    /// Returns a hint about whether the session may be expired/invalid.
    /// </summary>
    public static async Task<DiagnosticResult> CheckSessionAsync(WebViewService webViewService, ControlUiProbeSnapshot? snapshot = null)
    {
        if (!webViewService.IsInitialized)
        {
            return DiagnosticResult.Skip(StringResources.DiagnosticWebViewNotInitialized);
        }

        snapshot ??= await webViewService.InspectControlUiStateAsync();
        if (snapshot.Phase == ControlUiPhase.Unavailable)
        {
            return DiagnosticResult.Skip(
                StringResources.DiagnosticControlUiStateUnavailable,
                string.IsNullOrWhiteSpace(snapshot.Detail) ? null : snapshot.Detail);
        }

        return snapshot.Phase switch
        {
            ControlUiPhase.Connected => DiagnosticResult.Pass(StringResources.DiagnosticGatewaySessionAppearsActive),
            ControlUiPhase.PageLoaded or ControlUiPhase.GatewayConnecting => DiagnosticResult.Warn(
                snapshot.Summary,
                string.IsNullOrWhiteSpace(snapshot.Detail) ? StringResources.DiagnosticPageLoadedButEstablishing : snapshot.Detail),
            ControlUiPhase.AuthRequired => DiagnosticResult.Warn(snapshot.Summary, snapshot.DetailOrSummary),
            ControlUiPhase.PairingRequired => DiagnosticResult.Warn(
                snapshot.Summary,
                string.Format(StringResources.DiagnosticCurrentDeviceApprovalDetailFormat, snapshot.DetailOrSummary)),
            ControlUiPhase.OriginRejected => DiagnosticResult.Fail(
                snapshot.Summary,
                string.Format(StringResources.DiagnosticOriginRejectedFailDetailFormat, snapshot.DetailOrSummary)),
            ControlUiPhase.GatewayError => DiagnosticResult.Fail(snapshot.Summary, snapshot.DetailOrSummary),
            _ => DiagnosticResult.Skip(StringResources.DiagnosticNoPageLoaded)
        };
    }

    /// <summary>
    /// Runs all startup diagnostics and returns a summary.
    /// </summary>
    public static async Task<DiagnosticReport> RunAllAsync(string? gatewayUrl, WebViewService? webViewService)
    {
        var report = new DiagnosticReport();
        ControlUiProbeSnapshot? snapshot = null;

        report.Items.Add((StringResources.DiagnosticWebView2RuntimeLabel, CheckWebView2Runtime()));

        if (webViewService is not null)
        {
            snapshot = await webViewService.InspectControlUiStateAsync();
        }

        report.Items.Add((StringResources.DiagnosticNetworkConnectivityLabel, await ProbeNetworkAsync(gatewayUrl, snapshot)));

        if (webViewService is not null)
        {
            report.Items.Add((StringResources.DiagnosticSessionStatusLabel, await CheckSessionAsync(webViewService, snapshot)));
            report.Items.Add((StringResources.DiagnosticInstrumentationLabel, DescribeInstrumentation(webViewService)));
        }

        return report;
    }

    public static DiagnosticResult DescribeInstrumentation(WebViewService webViewService)
    {
        var summary =
            $"Inspect req={webViewService.TotalControlUiInspectionRequests}, " +
            $"cache={webViewService.CachedControlUiInspectionRequests}, " +
            $"coalesced={webViewService.CoalescedControlUiInspectionRequests}, " +
            $"hb reload={webViewService.HeartbeatRecoveryRequests}.";

        return DiagnosticResult.Pass(summary);
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
        })
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    private static bool IsLoopbackLike(Uri uri)
    {
        return uri.IsLoopback ||
            LocalLoopbackHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Result of a single diagnostic check.
/// </summary>
public record DiagnosticResult(DiagnosticStatus Status, string Message, string? Detail = null)
{
    public static DiagnosticResult Pass(string message) => new(DiagnosticStatus.Pass, message);
    public static DiagnosticResult Warn(string message, string? detail = null) => new(DiagnosticStatus.Warning, message, detail);
    public static DiagnosticResult Fail(string message, string? detail = null) => new(DiagnosticStatus.Fail, message, detail);
    public static DiagnosticResult Skip(string message, string? detail = null) => new(DiagnosticStatus.Skipped, message, detail);
}

public enum DiagnosticStatus
{
    Pass,
    Warning,
    Fail,
    Skipped,
}

/// <summary>
/// Aggregated diagnostic report.
/// </summary>
public class DiagnosticReport
{
    public List<(string Name, DiagnosticResult Result)> Items { get; } = [];

    public bool HasFailures => Items.Any(i => i.Result.Status == DiagnosticStatus.Fail);
    public bool HasWarnings => Items.Any(i => i.Result.Status == DiagnosticStatus.Warning);

    public string ToSummary()
    {
        var lines = new System.Text.StringBuilder();
        foreach (var (name, result) in Items)
        {
            var icon = result.Status switch
            {
                DiagnosticStatus.Pass => "[OK]",
                DiagnosticStatus.Warning => "[WARN]",
                DiagnosticStatus.Fail => "[FAIL]",
                _ => "[SKIP]",
            };

            lines.AppendLine($"{icon} {name}: {result.Message}");
            if (!string.IsNullOrEmpty(result.Detail))
            {
                lines.AppendLine($"   {result.Detail}");
            }
        }

        return lines.ToString();
    }
}
