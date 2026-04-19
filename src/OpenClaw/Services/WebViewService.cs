// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenClaw.Services;

/// <summary>
/// Manages WebView2 lifecycle, navigation, and connection state monitoring.
/// </summary>
public class WebViewService
{
    private const string ControlUiStatusMessageKind = "openclaw-control-ui-status";
    private const int DefaultHeartbeatFailureThreshold = 3;
    private const int DefaultHeartbeatConnectingThreshold = 3;
    private WebView2? _webView;
    private bool _isInitialized;
    private string? _lastNavigatedUrl;
    private int _retryCount;
    private CancellationTokenSource? _retryCts;
    private CancellationTokenSource? _statusProbeCts;
    private ControlUiProbeSnapshot _latestControlUiSnapshot = ControlUiProbeSnapshot.Unknown;
    private string? _lastReportedIssueKey;
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    // Heartbeat fields
    private PeriodicTimer? _heartbeatTimer;
    private CancellationTokenSource? _heartbeatCts;
    private int _heartbeatFailureCount;
    private int _heartbeatConnectingCount;
    private string? _lastHeartbeatObservationKey;
    private string? _heartbeatGatewayUrl;
    private int _heartbeatIntervalSeconds;
    private int _heartbeatFailureThreshold = DefaultHeartbeatFailureThreshold;
    private int _heartbeatConnectingThreshold = DefaultHeartbeatConnectingThreshold;
    private static readonly TimeSpan HeartbeatReloadCooldown = TimeSpan.FromSeconds(45);
    private static readonly HttpClient HeartbeatHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private DateTimeOffset _lastHeartbeatReloadAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Raised when the connection/loading state changes.
    /// </summary>
    public event Action<ConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Raised when a navigation error occurs.
    /// </summary>
    public event Action<string>? NavigationErrorOccurred;

    /// <summary>
    /// Raised when navigation completed successfully.
    /// </summary>
    public event Action<string?>? NavigationCompleted;

    /// <summary>
    /// Raised when the heartbeat decides the hosted Control UI should be refreshed.
    /// </summary>
    public event Action<string>? HeartbeatFailed;

    /// <summary>
    /// Raised when the heartbeat records a health observation for the hosted Control UI.
    /// </summary>
    public event Action<HeartbeatProbeResult>? HeartbeatObserved;

    /// <summary>
    /// Raised when the hosted Control UI reports an updated snapshot.
    /// </summary>
    public event Action<ControlUiProbeSnapshot>? ControlUiSnapshotUpdated;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public ConnectionState CurrentState { get; private set; } = ConnectionState.Offline;

    /// <summary>
    /// Gets whether the WebView2 control is initialized and ready.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets the environment profile currently backing the active WebView2 instance.
    /// </summary>
    public string? CurrentEnvironmentName { get; private set; }

    /// <summary>
    /// Gets the latest control UI probe snapshot observed from the hosted page.
    /// </summary>
    public ControlUiProbeSnapshot LatestControlUiSnapshot => _latestControlUiSnapshot;

    /// <summary>
    /// Initializes the WebView2 control with a custom user data folder.
    /// </summary>
    public async Task InitializeAsync(WebView2 webView, string environmentName)
    {
        DetachCurrentWebView();
        _webView = webView;
        CurrentEnvironmentName = environmentName;
        _isInitialized = false;

        try
        {
            var userDataFolder = GetUserDataFolderForEnvironment(environmentName);
            Directory.CreateDirectory(userDataFolder);

            // In WinUI 3, set user data folder via environment variable before initialization.
            // This avoids API signature differences between WinUI 3 and Win32 WebView2.
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);

            await _webView.EnsureCoreWebView2Async();
            // Make WebView2 follow system Light/Dark theme preferred scheme
            _webView.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Auto;
            
            // Set default background to transparent (blends with Mica)
            _webView.DefaultBackgroundColor = Microsoft.UI.Colors.Transparent;

            // Wire up events
            _webView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            _webView.CoreWebView2.ProcessFailed += OnProcessFailed;
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // Allow file input dialog
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;

            _webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;

            _isInitialized = true;
            App.Logger.Info("WebView2 initialized successfully.");
        }
        catch (Exception ex)
        {
            App.Logger.Error($"WebView2 initialization failed: {ex}");
            SetState(ConnectionState.Error);
            NavigationErrorOccurred?.Invoke($"WebView2 initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Navigates the WebView2 to the specified URL.
    /// </summary>
    public void Navigate(string url)
    {
        if (!_isInitialized || _webView?.CoreWebView2 is null)
        {
            App.Logger.Warning("Cannot navigate: WebView2 not initialized.");
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            App.Logger.Warning($"Invalid URL: {url}");
            NavigationErrorOccurred?.Invoke($"Invalid URL: {url}");
            return;
        }

        App.Logger.Info($"Navigating to: {url}");
        _lastNavigatedUrl = url;
        _retryCount = 0;
        CancelStatusProbeLoop();
        _retryCts?.Cancel();
        _retryCts = new CancellationTokenSource();
        SetState(ConnectionState.Loading);
        _webView.CoreWebView2.Navigate(url);
    }

    /// <summary>
    /// Reloads the current page.
    /// </summary>
    public void Reload()
    {
        if (_webView?.CoreWebView2 is null) return;
        App.Logger.Info("Reloading page.");
        SetState(ConnectionState.Loading);
        _webView.CoreWebView2.Reload();
    }

    /// <summary>
    /// Sends the in-app stop command when possible, falling back to stopping navigation.
    /// </summary>
    public async void Stop()
    {
        if (_webView?.CoreWebView2 is null) return;

        var aborted = await TryAbortActiveRunAsync();
        if (aborted)
        {
            App.Logger.Info("Triggered the hosted UI stop action.");
            return;
        }

        var injected = await InjectStopCommandAsync();
        if (injected)
        {
            App.Logger.Info("Injected /stop command into the web UI.");
            return;
        }

        App.Logger.Info("Stop command injection unavailable, stopping navigation instead.");
        _webView.CoreWebView2.Stop();

        if (CurrentState == ConnectionState.Loading)
        {
            SetState(ConnectionState.Offline);
        }
    }

    /// <summary>
    /// Clears all browsing data (cookies, cache, local storage) from the WebView2 profile.
    /// </summary>
    public async Task ClearBrowsingDataAsync()
    {
        if (_webView?.CoreWebView2 is null) return;

        try
        {
            App.Logger.Info("Clearing browsing data.");
            await _webView.CoreWebView2.Profile.ClearBrowsingDataAsync();
            App.Logger.Info("Browsing data cleared.");
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to clear browsing data: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the WebView2 DevTools window.
    /// </summary>
    public void OpenDevTools()
    {
        _webView?.CoreWebView2?.OpenDevToolsWindow();
    }

    /// <summary>
    /// Attempts to inspect the hosted Control UI state via the injected page bridge.
    /// </summary>
    public async Task<ControlUiProbeSnapshot> InspectControlUiStateAsync()
    {
        if (_webView?.CoreWebView2 is null)
        {
            return ControlUiProbeSnapshot.Unavailable("WebView2 is not initialized.");
        }

        const string script = """
(() => {
  if (!window.__openClawHostBridge || typeof window.__openClawHostBridge.inspect !== 'function') {
    return JSON.stringify({
      kind: 'openclaw-control-ui-status',
      phase: 'unavailable',
      summary: 'Control UI bridge unavailable.',
      detail: '',
      url: window.location ? window.location.href : '',
      shellDetected: false
    });
  }

  return JSON.stringify(window.__openClawHostBridge.inspect());
})()
""";

        try
        {
            var rawResult = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            var payload = JsonSerializer.Deserialize<string>(rawResult);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return _latestControlUiSnapshot;
            }

            var snapshot = ParseControlUiSnapshot(payload);
            ApplyControlUiSnapshot(snapshot, raiseIssueEvent: false);
            return snapshot;
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Failed to inspect hosted UI state: {ex.Message}");
            return ControlUiProbeSnapshot.Unavailable(ex.Message);
        }
    }

    /// <summary>
    /// Clears the session for a specific environment profile.
    /// </summary>
    public async Task ClearEnvironmentSessionAsync(string environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            return;
        }

        if (_webView?.CoreWebView2 is not null &&
            _isInitialized &&
            string.Equals(CurrentEnvironmentName, environmentName, StringComparison.Ordinal))
        {
            App.Logger.Info($"Clearing active browsing data for environment '{environmentName}'.");
            await _webView.CoreWebView2.Profile.ClearBrowsingDataAsync();
            return;
        }

        DeleteUserDataFolderForEnvironment(environmentName);
    }

    /// <summary>
    /// Attempts to inject "/stop" into the active chat input and submit it.
    /// </summary>
    public async Task<bool> InjectStopCommandAsync()
    {
        if (_webView?.CoreWebView2 is null)
        {
            return false;
        }

        const string script = """
(() => {
  const stopCommand = '/stop';

  const isVisible = (el) => {
    if (!el) return false;
    const style = window.getComputedStyle(el);
    if (style.display === 'none' || style.visibility === 'hidden') return false;
    const rect = el.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0;
  };

  const clearElement = (el) => {
    if (!el) return;

    if ('value' in el) {
      const prototype = Object.getPrototypeOf(el);
      const descriptor = Object.getOwnPropertyDescriptor(prototype, 'value');
      if (descriptor && typeof descriptor.set === 'function') {
        descriptor.set.call(el, '');
      } else {
        el.value = '';
      }
      el.dispatchEvent(new Event('input', { bubbles: true }));
      el.dispatchEvent(new Event('change', { bubbles: true }));
      return;
    }

    el.textContent = '';
    el.dispatchEvent(new InputEvent('input', { bubbles: true, data: '', inputType: 'deleteContentBackward' }));
  };

  const submitElement = (el) => {
    if (!el) return false;
    const form = el.closest('form');
    if (form) {
      const submitEvent = new Event('submit', { bubbles: true, cancelable: true });
      form.dispatchEvent(submitEvent);
      if (typeof form.requestSubmit === 'function') {
        form.requestSubmit();
      } else if (typeof form.submit === 'function') {
        form.submit();
      }
      window.setTimeout(() => clearElement(el), 0);
      return true;
    }

    const keyboardEventInit = {
      key: 'Enter',
      code: 'Enter',
      keyCode: 13,
      which: 13,
      bubbles: true,
      cancelable: true
    };

    el.dispatchEvent(new KeyboardEvent('keydown', keyboardEventInit));
    el.dispatchEvent(new KeyboardEvent('keypress', keyboardEventInit));
    el.dispatchEvent(new KeyboardEvent('keyup', keyboardEventInit));
    window.setTimeout(() => clearElement(el), 0);
    return true;
  };

  const setNativeValue = (el, value) => {
    const prototype = Object.getPrototypeOf(el);
    const descriptor = Object.getOwnPropertyDescriptor(prototype, 'value');
    if (descriptor && typeof descriptor.set === 'function') {
      descriptor.set.call(el, value);
    } else {
      el.value = value;
    }
  };

  const textInput = Array.from(document.querySelectorAll('textarea, input[type="text"], input:not([type])'))
    .find((el) => !el.disabled && !el.readOnly && isVisible(el));

  if (textInput) {
    textInput.focus();
    setNativeValue(textInput, stopCommand);
    textInput.dispatchEvent(new Event('input', { bubbles: true }));
    textInput.dispatchEvent(new Event('change', { bubbles: true }));
    return submitElement(textInput);
  }

  const editor = Array.from(document.querySelectorAll('[contenteditable="true"], [role="textbox"]'))
    .find((el) => !el.hasAttribute('disabled') && isVisible(el));

  if (editor) {
    editor.focus();
    editor.textContent = stopCommand;
    editor.dispatchEvent(new InputEvent('input', { bubbles: true, data: stopCommand, inputType: 'insertText' }));
    return submitElement(editor);
  }

  return false;
})()
""";

        try
        {
            var result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            return string.Equals(result?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Failed to inject /stop command: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Attempts to use the hosted UI's built-in stop/abort affordance before falling back to command injection.
    /// </summary>
    public async Task<bool> TryAbortActiveRunAsync()
    {
        if (_webView?.CoreWebView2 is null)
        {
            return false;
        }

        const string script = """
(() => {
  const isVisible = (el) => {
    if (!el) return false;
    const style = window.getComputedStyle(el);
    if (style.display === 'none' || style.visibility === 'hidden') return false;
    const rect = el.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0;
  };

  const labelOf = (el) => [
    el?.getAttribute?.('aria-label'),
    el?.getAttribute?.('title'),
    el?.innerText,
    el?.textContent
  ].filter(Boolean).join(' ').trim();

  const abortTargets = [
    window.chat,
    window.__openclaw?.chat,
    window.__OPENCLAW__?.chat,
    window.__APP__?.chat,
    window.app?.chat
  ];

  for (const target of abortTargets) {
    if (target && typeof target.abort === 'function') {
      target.abort();
      return true;
    }
  }

  const abortButton = Array.from(document.querySelectorAll('button, [role="button"], [aria-label], [title]'))
    .find((el) => isVisible(el) && /\b(stop|abort)\b/i.test(labelOf(el)));

  if (abortButton) {
    abortButton.click();
    return true;
  }

  return false;
})()
""";

        try
        {
            var result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            return string.Equals(result?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Failed to trigger hosted UI stop action: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Retries the last navigation if within retry limits.
    /// Returns true if a retry was initiated.
    /// </summary>
    public bool RetryNavigation()
    {
        if (string.IsNullOrEmpty(_lastNavigatedUrl) || !_isInitialized || _webView?.CoreWebView2 is null)
            return false;

        _retryCount = 0; // manual retry resets counter
        App.Logger.Info($"Manual retry navigation to: {_lastNavigatedUrl}");
        SetState(ConnectionState.Loading);
        _webView.CoreWebView2.Navigate(_lastNavigatedUrl);
        return true;
    }

    /// <summary>
    /// Gets the current source URL of the WebView2.
    /// </summary>
    public string? GetCurrentUrl()
    {
        return _webView?.CoreWebView2?.Source;
    }

    /// <summary>
    /// Gets whether the active WebView2 instance already uses the requested environment profile.
    /// </summary>
    public bool IsUsingEnvironmentProfile(string? environmentName)
    {
        return _isInitialized &&
            !string.IsNullOrWhiteSpace(environmentName) &&
            string.Equals(CurrentEnvironmentName, environmentName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Starts the periodic heartbeat probe against the given gateway URL.
    /// If the interval is 0 or negative, the heartbeat is disabled.
    /// </summary>
    public void StartHeartbeat(string gatewayUrl, int intervalSeconds)
    {
        var heartbeatSettings = App.Configuration.Settings.Heartbeat;
        if (!heartbeatSettings.EnableHeartbeat)
        {
            StopHeartbeat();
            App.Logger.Info("Heartbeat disabled in settings.");
            return;
        }

        if (intervalSeconds <= 0 || string.IsNullOrEmpty(gatewayUrl))
        {
            StopHeartbeat();
            App.Logger.Info("Heartbeat disabled (interval=0 or no URL).");
            return;
        }

        if (_heartbeatCts is not null &&
            _heartbeatTimer is not null &&
            string.Equals(_heartbeatGatewayUrl, gatewayUrl, StringComparison.OrdinalIgnoreCase) &&
            _heartbeatIntervalSeconds == intervalSeconds)
        {
            return;
        }

        StopHeartbeat();

        _heartbeatFailureCount = 0;
        _heartbeatConnectingCount = 0;
        _lastHeartbeatObservationKey = null;
        _heartbeatGatewayUrl = gatewayUrl;
        _heartbeatIntervalSeconds = intervalSeconds;
        _heartbeatFailureThreshold = Math.Max(1, heartbeatSettings.FailureThreshold);
        _heartbeatConnectingThreshold = Math.Max(1, heartbeatSettings.ConnectingThreshold);
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        App.Logger.Info($"Heartbeat started: interval={intervalSeconds}s, failureThreshold={_heartbeatFailureThreshold}, connectingThreshold={_heartbeatConnectingThreshold}, url={gatewayUrl}");
        _ = RunSessionAwareHeartbeatLoopAsync(gatewayUrl, _heartbeatCts.Token);
    }

    /// <summary>
    /// Stops the periodic heartbeat probe.
    /// </summary>
    public void StopHeartbeat()
    {
        if (_heartbeatCts is not null)
        {
            _heartbeatCts.Cancel();
            _heartbeatCts.Dispose();
            _heartbeatCts = null;
        }

        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
        _heartbeatFailureCount = 0;
        _heartbeatConnectingCount = 0;
        _lastHeartbeatObservationKey = null;
        _heartbeatGatewayUrl = null;
        _heartbeatIntervalSeconds = 0;
        _heartbeatFailureThreshold = DefaultHeartbeatFailureThreshold;
        _heartbeatConnectingThreshold = DefaultHeartbeatConnectingThreshold;
    }


    private async Task RunSessionAwareHeartbeatLoopAsync(string gatewayUrl, CancellationToken token)
    {
        try
        {
            while (await _heartbeatTimer!.WaitForNextTickAsync(token))
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                var probe = await ProbeGatewayHealthAsync(gatewayUrl, token);
                LogHeartbeatObservation(probe);

                if (probe.Status == HeartbeatProbeStatus.Healthy)
                {
                    if (_heartbeatFailureCount > 0)
                    {
                        App.Logger.Info($"Heartbeat recovered after {_heartbeatFailureCount} failure(s).");
                    }

                    _heartbeatFailureCount = 0;
                    _heartbeatConnectingCount = 0;
                    continue;
                }

                if (probe.Status == HeartbeatProbeStatus.SessionBlocked)
                {
                    if (_heartbeatFailureCount > 0)
                    {
                        App.Logger.Info("Heartbeat failure counter reset because the hosted UI requires user action.");
                    }

                    _heartbeatFailureCount = 0;
                    _heartbeatConnectingCount = 0;
                    continue;
                }

                if (probe.Status == HeartbeatProbeStatus.Connecting)
                {
                    _heartbeatFailureCount = 0;
                    _heartbeatConnectingCount++;

                    if (_heartbeatConnectingCount < _heartbeatConnectingThreshold)
                    {
                        continue;
                    }

                    if (TryScheduleHeartbeatReload(probe.Message, preserveConnectingCounter: true))
                    {
                        return;
                    }

                    continue;
                }

                _heartbeatConnectingCount = 0;
                _heartbeatFailureCount++;
                App.Logger.Warning($"Heartbeat failure {_heartbeatFailureCount}/{_heartbeatFailureThreshold}.");

                if (_heartbeatFailureCount >= _heartbeatFailureThreshold)
                {
                    if (TryScheduleHeartbeatReload(probe.Message))
                    {
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when StopHeartbeat() is called.
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Heartbeat loop error: {ex.Message}");
        }
    }

    private async Task<HeartbeatProbeResult> ProbeGatewayHealthAsync(string url, CancellationToken token)
    {
        var hostedSessionResult = await ProbeHostedSessionAsync();
        if (hostedSessionResult is not null)
        {
            if (hostedSessionResult.Status is HeartbeatProbeStatus.Failure or HeartbeatProbeStatus.Connecting)
            {
                var transportResult = await ProbeGatewayTransportAsync(url, token);
                if (transportResult.Status == HeartbeatProbeStatus.Healthy)
                {
                    return hostedSessionResult with
                    {
                        Message = $"{hostedSessionResult.Message} {transportResult.Message}"
                    };
                }
            }

            return hostedSessionResult;
        }

        return await ProbeGatewayTransportAsync(url, token);
    }

    private static async Task<HeartbeatProbeResult> ProbeGatewayTransportAsync(string url, CancellationToken token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache, no-store, max-age=0");
            request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml");

            using var response = await HeartbeatHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            var statusCode = (int)response.StatusCode;
            var proxyHint = response.Headers.TryGetValues("cf-ray", out _) ? " via Cloudflare" : string.Empty;

            return statusCode switch
            {
                >= 200 and < 300 => HeartbeatProbeResult.Healthy($"Gateway reachable over HTTP{proxyHint} ({statusCode})."),
                301 or 302 or 303 or 307 or 308 => HeartbeatProbeResult.Healthy(
                    $"Gateway reachable over HTTP{proxyHint} but redirected ({statusCode})."),
                401 or 403 => HeartbeatProbeResult.Healthy(
                    $"Gateway reachable over HTTP{proxyHint} but requires authentication or origin approval ({statusCode})."),
                404 => HeartbeatProbeResult.Healthy(
                    $"Gateway reachable over HTTP{proxyHint} but the configured Control UI path returned 404."),
                405 => HeartbeatProbeResult.Healthy(
                    $"Gateway reachable over HTTP{proxyHint} but the proxy rejected the probe method ({statusCode})."),
                _ => HeartbeatProbeResult.Healthy(
                    $"Gateway reachable over HTTP{proxyHint} ({statusCode} {response.ReasonPhrase}).")
            };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HeartbeatProbeResult.Failure($"Gateway heartbeat request failed: {ex.Message}");
        }
    }

    private async Task<HeartbeatProbeResult?> ProbeHostedSessionAsync()
    {
        if (!_isInitialized || _webView?.CoreWebView2 is null)
        {
            return null;
        }

        var snapshot = await InspectControlUiStateAsync();
        return snapshot.Phase switch
        {
            ControlUiPhase.Connected =>
                HeartbeatProbeResult.Healthy("Hosted Control UI reports an active Gateway session."),
            ControlUiPhase.AuthRequired or ControlUiPhase.PairingRequired or ControlUiPhase.OriginRejected =>
                HeartbeatProbeResult.SessionBlocked(snapshot.DetailOrSummary),
            ControlUiPhase.PageLoaded or ControlUiPhase.GatewayConnecting =>
                HeartbeatProbeResult.Connecting("Hosted Control UI is still reconnecting to the Gateway."),
            ControlUiPhase.GatewayError =>
                HeartbeatProbeResult.Failure(snapshot.DetailOrSummary),
            _ => null,
        };
    }

    private bool TryScheduleHeartbeatReload(string message, bool preserveConnectingCounter = false)
    {
        var elapsed = DateTimeOffset.UtcNow - _lastHeartbeatReloadAt;
        if (elapsed < HeartbeatReloadCooldown)
        {
            var remaining = HeartbeatReloadCooldown - elapsed;
            App.Logger.Warning($"Heartbeat auto-refresh suppressed for another {Math.Ceiling(remaining.TotalSeconds)}s to avoid reverse-proxy thrash.");
            _heartbeatFailureCount = _heartbeatFailureThreshold - 1;

            if (preserveConnectingCounter)
            {
                _heartbeatConnectingCount = _heartbeatConnectingThreshold - 1;
            }

            return false;
        }

        _lastHeartbeatReloadAt = DateTimeOffset.UtcNow;
        App.Logger.Warning($"Heartbeat threshold reached, requesting session recovery. Reason: {message}");
        HeartbeatFailed?.Invoke(message);

        // Stop heartbeat; coordinator-driven recovery will restart it after the session stabilizes.
        StopHeartbeat();
        return true;
    }

    private void LogHeartbeatObservation(HeartbeatProbeResult result)
    {
        HeartbeatObserved?.Invoke(result);

        var observationKey = $"{result.Status}:{result.Message}";
        if (string.Equals(_lastHeartbeatObservationKey, observationKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastHeartbeatObservationKey = observationKey;

        switch (result.Status)
        {
            case HeartbeatProbeStatus.Healthy:
                App.Logger.Info(result.Message);
                break;
            case HeartbeatProbeStatus.SessionBlocked:
                App.Logger.Warning($"Heartbeat detected a session issue that requires user action: {result.Message}");
                break;
            case HeartbeatProbeStatus.Connecting:
                App.Logger.Info(result.Message);
                break;
            case HeartbeatProbeStatus.Failure:
                App.Logger.Warning(result.Message);
                break;
        }
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var snapshot = ParseControlUiSnapshot(args.WebMessageAsJson);
            if (snapshot.Phase == ControlUiPhase.Unknown)
            {
                return;
            }

            ApplyControlUiSnapshot(snapshot, raiseIssueEvent: true);
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Failed to process Control UI status message: {ex.Message}");
        }
    }

    // --- Event handlers ---

    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        CancelStatusProbeLoop();
        _lastReportedIssueKey = null;
        _heartbeatConnectingCount = 0;
        _lastHeartbeatObservationKey = null;
        _latestControlUiSnapshot = ControlUiProbeSnapshot.Loading(args.Uri);
        SetState(ConnectionState.Loading);
    }

    private async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (args.IsSuccess)
        {
            _retryCount = 0;
            ApplyControlUiSnapshot(ControlUiProbeSnapshot.PageLoaded(sender.Source), raiseIssueEvent: false);
            StartStatusProbeLoop();
            NavigationCompleted?.Invoke(sender.Source);
        }
        else
        {
            CancelStatusProbeLoop();
            App.Logger.Warning($"Navigation failed: {args.WebErrorStatus}");

            var isConnectionError = args.WebErrorStatus is
                CoreWebView2WebErrorStatus.ConnectionAborted or
                CoreWebView2WebErrorStatus.ConnectionReset or
                CoreWebView2WebErrorStatus.Disconnected or
                CoreWebView2WebErrorStatus.Timeout or
                CoreWebView2WebErrorStatus.ServerUnreachable or
                CoreWebView2WebErrorStatus.HostNameNotResolved;

            if (isConnectionError)
            {
                SetState(ConnectionState.Reconnecting);

                // Auto-retry for connection errors
                if (_retryCount < MaxRetries && !string.IsNullOrEmpty(_lastNavigatedUrl))
                {
                    _retryCount++;
                    var token = _retryCts?.Token ?? CancellationToken.None;
                    App.Logger.Info($"Auto-retry {_retryCount}/{MaxRetries} in {RetryDelay.TotalSeconds}s...");
                    try
                    {
                        await Task.Delay(RetryDelay, token);
                    }
                    catch (TaskCanceledException)
                    {
                        App.Logger.Info("Auto-retry cancelled (new navigation started).");
                        return;
                    }
                    if (_webView?.CoreWebView2 is not null && !string.IsNullOrEmpty(_lastNavigatedUrl))
                    {
                        _webView.CoreWebView2.Navigate(_lastNavigatedUrl);
                        return; // don't fire error event for auto-retries
                    }
                }
            }

            SetState(args.WebErrorStatus switch
            {
                CoreWebView2WebErrorStatus.CertificateCommonNameIsIncorrect or
                CoreWebView2WebErrorStatus.CertificateExpired or
                CoreWebView2WebErrorStatus.CertificateRevoked or
                CoreWebView2WebErrorStatus.CertificateIsInvalid => ConnectionState.AuthFailed,
                _ when isConnectionError => ConnectionState.Reconnecting,
                _ => ConnectionState.Error,
            });
            NavigationErrorOccurred?.Invoke($"Navigation error: {args.WebErrorStatus}");
        }
    }

    private void OnProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
    {
        CancelStatusProbeLoop();
        _latestControlUiSnapshot = ControlUiProbeSnapshot.Unavailable("Browser process failed.");
        App.Logger.Error($"WebView2 process failed: {args.Reason} ({args.ProcessFailedKind})");
        SetState(ConnectionState.Error);
        NavigationErrorOccurred?.Invoke($"Browser process failed: {args.Reason}");
    }

    private void DetachCurrentWebView()
    {
        CancelStatusProbeLoop();
        StopHeartbeat();
        _retryCts?.Cancel();

        if (_webView?.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.NavigationStarting -= OnNavigationStarting;
            _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            _webView.CoreWebView2.ProcessFailed -= OnProcessFailed;
            _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        }

        _webView = null;
        _isInitialized = false;
        _latestControlUiSnapshot = ControlUiProbeSnapshot.Unknown;
    }

    public void Dispose()
    {
        DetachCurrentWebView();
        _retryCts?.Dispose();
        _retryCts = null;
    }

    private void StartStatusProbeLoop()
    {
        CancelStatusProbeLoop();
        _statusProbeCts = new CancellationTokenSource();
        _ = ProbeControlUiStateAfterNavigationAsync(_statusProbeCts.Token);
    }

    private void CancelStatusProbeLoop()
    {
        if (_statusProbeCts is not null)
        {
            _statusProbeCts.Cancel();
            _statusProbeCts.Dispose();
            _statusProbeCts = null;
        }
    }

    private async Task ProbeControlUiStateAfterNavigationAsync(CancellationToken token)
    {
        var delays = new[]
        {
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(8),
        };

        try
        {
            foreach (var delay in delays)
            {
                await Task.Delay(delay, token);
                var snapshot = await InspectControlUiStateAsync();
                if (snapshot.IsTerminal)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when navigation changes.
        }
    }

    private void ApplyControlUiSnapshot(ControlUiProbeSnapshot snapshot, bool raiseIssueEvent)
    {
        _latestControlUiSnapshot = snapshot;
        ControlUiSnapshotUpdated?.Invoke(snapshot);

        switch (snapshot.Phase)
        {
            case ControlUiPhase.Loading:
                SetState(ConnectionState.Loading);
                break;
            case ControlUiPhase.PageLoaded:
            case ControlUiPhase.GatewayConnecting:
                SetState(ConnectionState.GatewayConnecting);
                break;
            case ControlUiPhase.Connected:
                _lastReportedIssueKey = null;
                SetState(ConnectionState.Connected);
                break;
            case ControlUiPhase.AuthRequired:
                SetState(ConnectionState.AuthFailed);
                break;
            case ControlUiPhase.PairingRequired:
            case ControlUiPhase.OriginRejected:
            case ControlUiPhase.GatewayError:
                SetState(ConnectionState.Error);
                break;
            case ControlUiPhase.Unavailable:
            case ControlUiPhase.Unknown:
            default:
                break;
        }

        if (!raiseIssueEvent || !snapshot.IsIssue)
        {
            return;
        }

        if (string.Equals(snapshot.IssueKey, _lastReportedIssueKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastReportedIssueKey = snapshot.IssueKey;
        NavigationErrorOccurred?.Invoke(snapshot.DetailOrSummary);
    }

    private static ControlUiProbeSnapshot ParseControlUiSnapshot(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.String)
        {
            var nested = root.GetString();
            return string.IsNullOrWhiteSpace(nested)
                ? ControlUiProbeSnapshot.Unknown
                : ParseControlUiSnapshot(nested);
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return ControlUiProbeSnapshot.Unknown;
        }

        var kind = GetString(root, "kind");
        if (!string.Equals(kind, ControlUiStatusMessageKind, StringComparison.Ordinal))
        {
            return ControlUiProbeSnapshot.Unknown;
        }

        var phase = ParsePhase(GetString(root, "phase"));
        var summary = GetString(root, "summary");
        var detail = GetString(root, "detail");
        var url = GetString(root, "url");
        var shellDetected = root.TryGetProperty("shellDetected", out var shellProperty) &&
            shellProperty.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            shellProperty.GetBoolean();
        var isBusy = root.TryGetProperty("isBusy", out var busyProperty) &&
            busyProperty.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            busyProperty.GetBoolean();
        var inputFocused = root.TryGetProperty("inputFocused", out var inputFocusedProperty) &&
            inputFocusedProperty.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            inputFocusedProperty.GetBoolean();
        var workState = GetString(root, "workState");
        var currentModel = GetString(root, "currentModel");
        return new ControlUiProbeSnapshot(phase, summary, detail, url, shellDetected, isBusy, inputFocused, workState, currentModel);
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static ControlUiPhase ParsePhase(string value)
    {
        return value switch
        {
            "loading" => ControlUiPhase.Loading,
            "page_loaded" => ControlUiPhase.PageLoaded,
            "gateway_connecting" => ControlUiPhase.GatewayConnecting,
            "connected" => ControlUiPhase.Connected,
            "auth_required" => ControlUiPhase.AuthRequired,
            "pairing_required" => ControlUiPhase.PairingRequired,
            "origin_rejected" => ControlUiPhase.OriginRejected,
            "gateway_error" => ControlUiPhase.GatewayError,
            "unavailable" => ControlUiPhase.Unavailable,
            _ => ControlUiPhase.Unknown,
        };
    }

    private void SetState(ConnectionState newState)
    {
        if (CurrentState != newState)
        {
            CurrentState = newState;
            ConnectionStateChanged?.Invoke(newState);
        }
    }

    public static string GetUserDataFolderForEnvironment(string environmentName)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClaw",
            "WebView2Data");

        return Path.Combine(root, BuildEnvironmentFolderName(environmentName));
    }

    public static void DeleteUserDataFolderForEnvironment(string environmentName)
    {
        try
        {
            var folder = GetUserDataFolderForEnvironment(environmentName);
            if (!Directory.Exists(folder))
            {
                return;
            }

            Directory.Delete(folder, recursive: true);
            App.Logger.Info($"Deleted WebView2 profile folder for environment '{environmentName}'.");
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Failed to delete WebView2 profile folder for environment '{environmentName}': {ex.Message}");
        }
    }

    public static void TryMoveUserDataFolderToRenamedEnvironment(string originalEnvironmentName, string renamedEnvironmentName)
    {
        if (string.IsNullOrWhiteSpace(originalEnvironmentName) ||
            string.IsNullOrWhiteSpace(renamedEnvironmentName) ||
            string.Equals(originalEnvironmentName, renamedEnvironmentName, StringComparison.Ordinal))
        {
            return;
        }

        var sourceFolder = GetUserDataFolderForEnvironment(originalEnvironmentName);
        var targetFolder = GetUserDataFolderForEnvironment(renamedEnvironmentName);

        try
        {
            if (!Directory.Exists(sourceFolder) || Directory.Exists(targetFolder))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetFolder)!);
            Directory.Move(sourceFolder, targetFolder);
            App.Logger.Info($"Moved WebView2 profile folder from '{originalEnvironmentName}' to '{renamedEnvironmentName}'.");
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Failed to move WebView2 profile folder from '{originalEnvironmentName}' to '{renamedEnvironmentName}': {ex.Message}");
        }
    }

    private static string BuildEnvironmentFolderName(string environmentName)
    {
        var normalized = string.IsNullOrWhiteSpace(environmentName) ? "default" : environmentName.Trim();
        var sanitized = new string(normalized
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "default";
        }

        sanitized = sanitized.Length > 48 ? sanitized[..48] : sanitized;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..8];
        return $"{sanitized}_{hash}";
    }
}

/// <summary>
/// Represents the connection/loading state of the WebView2 session.
/// </summary>
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
