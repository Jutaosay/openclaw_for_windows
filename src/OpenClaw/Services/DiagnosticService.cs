// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.Web.WebView2.Core;

namespace OpenClaw.Services;

/// <summary>
/// Performs startup diagnostics: WebView2 runtime detection, network probes,
/// and auth/session invalidation detection.
/// </summary>
public class DiagnosticService
{
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
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
    public static async Task<DiagnosticResult> ProbeNetworkAsync(string? gatewayUrl)
    {
        if (string.IsNullOrEmpty(gatewayUrl))
        {
            return DiagnosticResult.Skip("No gateway URL configured.");
        }

        try
        {
            var response = await SharedHttpClient.GetAsync(gatewayUrl);

            return response.IsSuccessStatusCode
                ? DiagnosticResult.Pass($"Gateway reachable ({(int)response.StatusCode})")
                : DiagnosticResult.Warn($"Gateway returned {(int)response.StatusCode} {response.ReasonPhrase}");
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
    public static async Task<DiagnosticResult> CheckSessionAsync(WebViewService webViewService)
    {
        if (!webViewService.IsInitialized)
        {
            return DiagnosticResult.Skip("WebView2 not initialized.");
        }

        // This is a best-effort check — looks for common "login" or "auth" indicators
        // in the current page URL or title.
        var currentUrl = webViewService.GetCurrentUrl();
        if (string.IsNullOrEmpty(currentUrl))
        {
            return DiagnosticResult.Skip("No page loaded.");
        }

        var lowerUrl = currentUrl.ToLowerInvariant();
        if (lowerUrl.Contains("login") || lowerUrl.Contains("auth") || lowerUrl.Contains("signin"))
        {
            return DiagnosticResult.Warn(
                "Session may be expired",
                "The current page appears to be a login/auth page. You may need to re-authenticate.");
        }

        return DiagnosticResult.Pass("Session appears active.");
    }

    /// <summary>
    /// Runs all startup diagnostics and returns a summary.
    /// </summary>
    public static async Task<DiagnosticReport> RunAllAsync(string? gatewayUrl, WebViewService? webViewService)
    {
        var report = new DiagnosticReport();

        report.Items.Add(("WebView2 Runtime", CheckWebView2Runtime()));
        report.Items.Add(("Network Connectivity", await ProbeNetworkAsync(gatewayUrl)));

        if (webViewService is not null)
        {
            report.Items.Add(("Session Status", await CheckSessionAsync(webViewService)));
        }

        return report;
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
    public static DiagnosticResult Skip(string message) => new(DiagnosticStatus.Skipped, message);
}

public enum DiagnosticStatus { Pass, Warning, Fail, Skipped }

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
                DiagnosticStatus.Pass => "✅",
                DiagnosticStatus.Warning => "⚠️",
                DiagnosticStatus.Fail => "❌",
                _ => "⏭️",
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
