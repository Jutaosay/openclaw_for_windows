// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.Web.WebView2.Core;
namespace OpenClaw.Services;

/// <summary>
/// Performs startup diagnostics: WebView2 runtime detection, network probes,
/// and auth/session invalidation detection.
/// </summary>
public class DiagnosticService
{
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

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
                    "WebView2 Runtime not found",
                    "Please install the WebView2 Evergreen Runtime from https://developer.microsoft.com/en-us/microsoft-edge/webview2/");
            }

            return DiagnosticResult.Pass($"WebView2 Runtime v{version}");
        }
        catch (Exception ex)
        {
            return DiagnosticResult.Fail(
                "WebView2 Runtime check failed",
                $"Error: {ex.Message}. Please install the WebView2 Evergreen Runtime.");
        }
    }

    /// <summary>
    /// Probes network connectivity to the given gateway URL.
    /// </summary>
    public static async Task<DiagnosticResult> ProbeNetworkAsync(string? gatewayUrl, ControlUiProbeSnapshot? snapshot = null)
    {
        if (string.IsNullOrEmpty(gatewayUrl))
        {
            return DiagnosticResult.Skip("No gateway URL configured.");
        }

        if (snapshot is not null && snapshot.Phase is not ControlUiPhase.Unknown and not ControlUiPhase.Unavailable)
        {
            return snapshot.Phase switch
            {
                ControlUiPhase.Connected => DiagnosticResult.Pass("Control UI reachable and Gateway session is active."),
                ControlUiPhase.Loading => DiagnosticResult.Warn(
                    "Control UI is still loading.",
                    "Reachability is known only after the hosted page reports its Gateway session state."),
                ControlUiPhase.PageLoaded or ControlUiPhase.GatewayConnecting => DiagnosticResult.Warn(
                    "Control UI reachable, but the Gateway session is still being established.",
                    snapshot.DetailOrSummary),
                ControlUiPhase.AuthRequired => DiagnosticResult.Warn(
                    "Control UI reachable, but authentication is required.",
                    snapshot.DetailOrSummary),
                ControlUiPhase.PairingRequired => DiagnosticResult.Warn(
                    "Control UI reachable, but device pairing is required.",
                    snapshot.DetailOrSummary),
                ControlUiPhase.OriginRejected => DiagnosticResult.Warn(
                    "Control UI reachable, but the remote origin was rejected.",
                    snapshot.DetailOrSummary),
                ControlUiPhase.GatewayError => DiagnosticResult.Warn(
                    "Control UI reachable, but the Gateway WebSocket session is failing.",
                    snapshot.DetailOrSummary),
                _ => DiagnosticResult.Skip("Control UI state is not available yet.")
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
                        $"Control UI HTTP endpoint reachable ({statusCode})",
                        "HTTP reachability alone does not confirm the Gateway WebSocket/session is healthy. Load the page and re-run diagnostics for a session-aware result."),
                401 or 403 =>
                    DiagnosticResult.Warn(
                        $"Control UI reachable but access was rejected ({statusCode})",
                        "The endpoint is up, but authentication, password, token, or origin validation blocked the session."),
                405 =>
                    DiagnosticResult.Warn(
                        "Control UI reachable but the request method/path was rejected (405)",
                        "The server responded, but the Control UI route or reverse-proxy method handling may be misconfigured."),
                301 or 302 or 303 or 307 or 308 =>
                    DiagnosticResult.Warn(
                        $"Control UI reachable but redirected ({statusCode})",
                        "Verify the configured Control UI URL, gateway.controlUi.basePath, and any reverse-proxy rewrite rules."),
                404 =>
                    DiagnosticResult.Warn(
                        "Control UI reachable but the requested path was not found (404)",
                        "This often means the configured URL is missing the correct base path."),
                >= 500 =>
                    DiagnosticResult.Fail(
                        $"Gateway returned {statusCode} {response.ReasonPhrase}",
                        "The remote endpoint is responding with a server-side failure before the hosted UI can establish its session."),
                _ =>
                    DiagnosticResult.Warn(
                        $"Gateway returned {statusCode} {response.ReasonPhrase}",
                        "The endpoint responded, but the Gateway session/auth state still needs to be verified from the hosted UI.")
            };
        }
        catch (TaskCanceledException)
        {
            return DiagnosticResult.Fail("Gateway timeout", "Connection timed out after 10 seconds.");
        }
        catch (HttpRequestException ex)
        {
            return DiagnosticResult.Fail("Gateway unreachable", ex.Message);
        }
        catch (Exception ex)
        {
            return DiagnosticResult.Fail("Network probe failed", ex.Message);
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
            return DiagnosticResult.Skip("WebView2 not initialized.");
        }

        snapshot ??= await webViewService.InspectControlUiStateAsync();
        if (snapshot.Phase == ControlUiPhase.Unavailable)
        {
            return DiagnosticResult.Skip(
                "Control UI state is not available yet.",
                string.IsNullOrWhiteSpace(snapshot.Detail) ? null : snapshot.Detail);
        }

        return snapshot.Phase switch
        {
            ControlUiPhase.Connected => DiagnosticResult.Pass("Gateway session appears active."),
            ControlUiPhase.PageLoaded or ControlUiPhase.GatewayConnecting => DiagnosticResult.Warn(
                snapshot.Summary,
                string.IsNullOrWhiteSpace(snapshot.Detail) ? "The page is loaded, but the Gateway session is still being established." : snapshot.Detail),
            ControlUiPhase.AuthRequired => DiagnosticResult.Warn(snapshot.Summary, snapshot.DetailOrSummary),
            ControlUiPhase.PairingRequired => DiagnosticResult.Warn(snapshot.Summary, snapshot.DetailOrSummary),
            ControlUiPhase.OriginRejected => DiagnosticResult.Fail(snapshot.Summary, snapshot.DetailOrSummary),
            ControlUiPhase.GatewayError => DiagnosticResult.Fail(snapshot.Summary, snapshot.DetailOrSummary),
            _ => DiagnosticResult.Skip("No page loaded.")
        };
    }

    /// <summary>
    /// Runs all startup diagnostics and returns a summary.
    /// </summary>
    public static async Task<DiagnosticReport> RunAllAsync(string? gatewayUrl, WebViewService? webViewService)
    {
        var report = new DiagnosticReport();
        ControlUiProbeSnapshot? snapshot = null;

        report.Items.Add(("WebView2 Runtime", CheckWebView2Runtime()));

        if (webViewService is not null)
        {
            snapshot = await webViewService.InspectControlUiStateAsync();
        }

        report.Items.Add(("Network Connectivity", await ProbeNetworkAsync(gatewayUrl, snapshot)));

        if (webViewService is not null)
        {
            report.Items.Add(("Session Status", await CheckSessionAsync(webViewService, snapshot)));
        }

        return report;
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
