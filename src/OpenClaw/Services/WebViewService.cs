// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace OpenClaw.Services;

/// <summary>
/// Manages WebView2 lifecycle, navigation, and connection state monitoring.
/// </summary>
public class WebViewService
{
    private WebView2? _webView;
    private bool _isInitialized;
    private string? _lastNavigatedUrl;
    private int _retryCount;
    private CancellationTokenSource? _retryCts;
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    // Heartbeat fields
    private PeriodicTimer? _heartbeatTimer;
    private CancellationTokenSource? _heartbeatCts;
    private int _heartbeatFailureCount;
    private const int HeartbeatFailureThreshold = 3;
    private static readonly HttpClient HeartbeatHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// Raised when the connection/loading state changes.
    /// </summary>
    public event Action<ConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Raised when a navigation error occurs.
    /// </summary>
    public event Action<string>? NavigationErrorOccurred;

    /// <summary>
    /// Raised when the heartbeat detects the gateway is unreachable (after threshold failures).
    /// </summary>
    public event Action? HeartbeatFailed;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public ConnectionState CurrentState { get; private set; } = ConnectionState.Offline;

    /// <summary>
    /// Gets whether the WebView2 control is initialized and ready.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Initializes the WebView2 control with a custom user data folder.
    /// </summary>
    public async Task InitializeAsync(WebView2 webView)
    {
        _webView = webView;

        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenClaw", "WebView2Data");
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
    /// Starts the periodic heartbeat probe against the given gateway URL.
    /// If the interval is 0 or negative, the heartbeat is disabled.
    /// </summary>
    public void StartHeartbeat(string gatewayUrl, int intervalSeconds)
    {
        StopHeartbeat();

        if (intervalSeconds <= 0 || string.IsNullOrEmpty(gatewayUrl))
        {
            App.Logger.Info("Heartbeat disabled (interval=0 or no URL).");
            return;
        }

        _heartbeatFailureCount = 0;
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        App.Logger.Info($"Heartbeat started: interval={intervalSeconds}s, url={gatewayUrl}");
        _ = RunHeartbeatLoopAsync(gatewayUrl, _heartbeatCts.Token);
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
    }

    private async Task RunHeartbeatLoopAsync(string gatewayUrl, CancellationToken token)
    {
        try
        {
            while (await _heartbeatTimer!.WaitForNextTickAsync(token))
            {
                if (token.IsCancellationRequested) break;

                var isAlive = await ProbeGatewayAsync(gatewayUrl);

                if (isAlive)
                {
                    if (_heartbeatFailureCount > 0)
                    {
                        App.Logger.Info($"Heartbeat recovered after {_heartbeatFailureCount} failure(s).");
                    }
                    _heartbeatFailureCount = 0;
                }
                else
                {
                    _heartbeatFailureCount++;
                    App.Logger.Warning($"Heartbeat failure {_heartbeatFailureCount}/{HeartbeatFailureThreshold}.");

                    if (_heartbeatFailureCount >= HeartbeatFailureThreshold)
                    {
                        App.Logger.Warning("Heartbeat threshold reached — triggering auto-reconnect.");
                        HeartbeatFailed?.Invoke();

                        // Stop heartbeat; it will restart after successful reconnect
                        StopHeartbeat();
                        Reload();
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when StopHeartbeat() is called
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Heartbeat loop error: {ex.Message}");
        }
    }

    private static async Task<bool> ProbeGatewayAsync(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await HeartbeatHttpClient.SendAsync(request);
            // Any response (even 4xx) means the server is reachable
            return true;
        }
        catch
        {
            return false;
        }
    }

    // --- Event handlers ---

    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        SetState(ConnectionState.Loading);
    }

    private async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (args.IsSuccess)
        {
            _retryCount = 0;
            SetState(ConnectionState.Connected);
        }
        else
        {
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
        App.Logger.Error($"WebView2 process failed: {args.Reason} ({args.ProcessFailedKind})");
        SetState(ConnectionState.Error);
        NavigationErrorOccurred?.Invoke($"Browser process failed: {args.Reason}");
    }

    private void SetState(ConnectionState newState)
    {
        if (CurrentState != newState)
        {
            CurrentState = newState;
            ConnectionStateChanged?.Invoke(newState);
        }
    }
}

/// <summary>
/// Represents the connection/loading state of the WebView2 session.
/// </summary>
public enum ConnectionState
{
    Offline,
    Loading,
    Connected,
    Reconnecting,
    AuthFailed,
    Error,
}
