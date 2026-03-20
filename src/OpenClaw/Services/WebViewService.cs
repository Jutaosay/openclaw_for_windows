// Copyright (c) OpenClaw. All rights reserved.

using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace OpenClaw.Services;

/// <summary>
/// Manages WebView2 lifecycle, navigation, JavaScript injection,
/// and connection state monitoring.
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

    /// <summary>
    /// Raised when the connection/loading state changes.
    /// </summary>
    public event Action<ConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Raised when a navigation error occurs.
    /// </summary>
    public event Action<string>? NavigationErrorOccurred;

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
    /// Stops the current navigation or page load.
    /// </summary>
    public void StopNavigation()
    {
        _webView?.CoreWebView2?.Stop();
    }

    /// <summary>
    /// Attempts to send a stop command to the remote OpenClaw UI via JavaScript injection.
    /// This is the MVP stop implementation: it injects "/stop" into the chat input.
    /// </summary>
    public async Task<bool> InjectStopCommandAsync()
    {
        if (_webView?.CoreWebView2 is null) return false;

        try
        {
            // MVP stop: try to find the chat input textarea and inject /stop
            // This is intentionally isolated so it can be replaced with a cleaner
            // API integration when OpenClaw Control provides one.
            const string script = """
                (function() {
                    // Attempt 1: Find textarea or contenteditable input
                    const textarea = document.querySelector('textarea') 
                        || document.querySelector('[contenteditable="true"]');
                    if (textarea) {
                        // Set value and dispatch input event
                        if (textarea.tagName === 'TEXTAREA' || textarea.tagName === 'INPUT') {
                            const nativeInputValueSetter = Object.getOwnPropertyDescriptor(
                                window.HTMLTextAreaElement.prototype, 'value')?.set 
                                || Object.getOwnPropertyDescriptor(
                                    window.HTMLInputElement.prototype, 'value')?.set;
                            if (nativeInputValueSetter) {
                                nativeInputValueSetter.call(textarea, '/stop');
                            } else {
                                textarea.value = '/stop';
                            }
                        } else {
                            textarea.textContent = '/stop';
                        }
                        textarea.dispatchEvent(new Event('input', { bubbles: true }));
                        
                        // Try to find and click send button
                        setTimeout(() => {
                            const sendBtn = document.querySelector(
                                'button[type="submit"], button[aria-label*="send"], button[aria-label*="Send"]');
                            if (sendBtn) sendBtn.click();
                        }, 100);
                        return 'stop_injected';
                    }
                    return 'input_not_found';
                })();
                """;

            var result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            App.Logger.Info($"Stop command injection result: {result}");
            return result.Contains("stop_injected");
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Stop command injection failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Injects an arbitrary quick command into the remote OpenClaw UI.
    /// Reuses the same textarea-injection pattern as stop.
    /// </summary>
    public async Task<bool> InjectQuickCommandAsync(string command)
    {
        if (_webView?.CoreWebView2 is null) return false;

        try
        {
            var escapedCommand = command.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"" , "\\\"");
            var script = $@"
                (function() {{
                    const textarea = document.querySelector('textarea')
                        || document.querySelector('[contenteditable=""true""]');
                    if (textarea) {{
                        if (textarea.tagName === 'TEXTAREA' || textarea.tagName === 'INPUT') {{
                            const setter = Object.getOwnPropertyDescriptor(
                                window.HTMLTextAreaElement.prototype, 'value')?.set
                                || Object.getOwnPropertyDescriptor(
                                    window.HTMLInputElement.prototype, 'value')?.set;
                            if (setter) setter.call(textarea, '{escapedCommand}');
                            else textarea.value = '{escapedCommand}';
                        }} else {{
                            textarea.textContent = '{escapedCommand}';
                        }}
                        textarea.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        setTimeout(() => {{
                            const sendBtn = document.querySelector(
                                'button[type=""submit""], button[aria-label*=""send""], button[aria-label*=""Send""]');
                            if (sendBtn) sendBtn.click();
                        }}, 100);
                        return 'command_injected';
                    }}
                    return 'input_not_found';
                }})();";

            var result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
            App.Logger.Info($"Quick command '{command}' injection result: {result}");
            return result.Contains("command_injected");
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Quick command injection failed: {ex.Message}");
            return false;
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
