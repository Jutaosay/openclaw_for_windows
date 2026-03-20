// Copyright (c) OpenClaw. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.UI.Xaml.Controls;
using OpenClaw.Helpers;
using OpenClaw.Models;
using OpenClaw.Services;

namespace OpenClaw.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Manages environment selection, WebView2 commands, and connection state.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly WebViewService _webViewService = new();
    private EnvironmentConfig? _selectedEnvironment;
    private string _statusMessage = StringResources.StatusOffline;
    private string _statusIcon = "\uE871"; // Globe icon
    private ConnectionState _connectionState = ConnectionState.Offline;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private bool _isErrorVisible;
    private bool _showRetryButton;
    private string _diagnosticSummary = string.Empty;
    private bool _isDiagnosticVisible;

    public MainViewModel()
    {
        // Initialize commands
        ReloadCommand = new SimpleCommand(OnReload);
        StopCommand = new SimpleCommand(async () => await OnStopAsync());
        ClearSessionCommand = new SimpleCommand(async () => await OnClearSessionAsync());
        OpenSettingsCommand = new SimpleCommand(() => OpenSettingsRequested?.Invoke());
        RetryCommand = new SimpleCommand(OnRetry);
        DevToolsCommand = new SimpleCommand(OnDevTools);
        QuickStopCommand = new SimpleCommand(async () => await OnQuickCommandAsync("/stop"));
        QuickStatusCommand = new SimpleCommand(async () => await OnQuickCommandAsync("/status"));
        QuickNewCommand = new SimpleCommand(async () => await OnQuickCommandAsync("/new"));
        QuickQueueCommand = new SimpleCommand(async () => await OnQuickCommandAsync("/queue"));
        RunDiagnosticsCommand = new SimpleCommand(async () => await OnRunDiagnosticsAsync());
        ViewLogsCommand = new SimpleCommand(() => ViewLogsRequested?.Invoke());

        // Wire up service events
        _webViewService.ConnectionStateChanged += OnConnectionStateChanged;
        _webViewService.NavigationErrorOccurred += OnNavigationError;

        // Load environments
        LoadEnvironments();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when the user requests to open the settings dialog.
    /// </summary>
    public event Action? OpenSettingsRequested;

    /// <summary>
    /// Raised when the user requests to view logs.
    /// </summary>
    public event Action? ViewLogsRequested;

    /// <summary>
    /// Raised when a navigation error occurs, for display to the user.
    /// </summary>
    public event Action<string>? ErrorOccurred;

    // --- Properties ---

    public ObservableCollection<EnvironmentConfig> Environments { get; } = [];

    public EnvironmentConfig? SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            if (_selectedEnvironment != value)
            {
                _selectedEnvironment = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentUrl));
                OnEnvironmentChanged();
            }
        }
    }

    public string CurrentUrl => _selectedEnvironment?.GatewayUrl ?? string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string StatusIcon
    {
        get => _statusIcon;
        private set { _statusIcon = value; OnPropertyChanged(); }
    }

    public ConnectionState ConnectionState
    {
        get => _connectionState;
        private set { _connectionState = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { _errorMessage = value; OnPropertyChanged(); }
    }

    public bool IsErrorVisible
    {
        get => _isErrorVisible;
        private set { _isErrorVisible = value; OnPropertyChanged(); }
    }

    public bool ShowRetryButton
    {
        get => _showRetryButton;
        private set { _showRetryButton = value; OnPropertyChanged(); }
    }

    public string DiagnosticSummary
    {
        get => _diagnosticSummary;
        private set { _diagnosticSummary = value; OnPropertyChanged(); }
    }

    public bool IsDiagnosticVisible
    {
        get => _isDiagnosticVisible;
        private set { _isDiagnosticVisible = value; OnPropertyChanged(); }
    }

    // --- Commands ---

    public ICommand ReloadCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ClearSessionCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand DevToolsCommand { get; }
    public ICommand QuickStopCommand { get; }
    public ICommand QuickStatusCommand { get; }
    public ICommand QuickNewCommand { get; }
    public ICommand QuickQueueCommand { get; }
    public ICommand RunDiagnosticsCommand { get; }
    public ICommand ViewLogsCommand { get; }

    // --- Public methods ---

    /// <summary>
    /// Gets the underlying WebViewService for binding to the WebView2 control.
    /// </summary>
    public WebViewService WebViewService => _webViewService;

    /// <summary>
    /// Initializes the WebView2 control. Called from the view after the control is loaded.
    /// </summary>
    public async Task InitializeWebViewAsync(WebView2 webView)
    {
        await _webViewService.InitializeAsync(webView);

        if (_webViewService.IsInitialized && _selectedEnvironment is not null)
        {
            _webViewService.Navigate(_selectedEnvironment.GatewayUrl);
        }
    }

    /// <summary>
    /// Reloads environments from configuration (e.g., after settings dialog closes).
    /// </summary>
    public void RefreshEnvironments()
    {
        LoadEnvironments();
    }

    // --- Private methods ---

    private void LoadEnvironments()
    {
        var settings = App.Configuration.Settings;
        Environments.Clear();

        foreach (var env in settings.Environments)
        {
            Environments.Add(env);
        }

        // Restore selection
        _selectedEnvironment = App.Configuration.GetSelectedEnvironment();
        OnPropertyChanged(nameof(SelectedEnvironment));
        OnPropertyChanged(nameof(CurrentUrl));
    }

    private void OnEnvironmentChanged()
    {
        if (_selectedEnvironment is null) return;

        // Persist selection
        App.Configuration.Settings.SelectedEnvironmentName = _selectedEnvironment.Name;
        App.Configuration.Save();

        // Navigate
        if (_webViewService.IsInitialized)
        {
            _webViewService.Navigate(_selectedEnvironment.GatewayUrl);
        }
    }

    private void OnReload()
    {
        _webViewService.Reload();
    }

    private async Task OnStopAsync()
    {
        App.Logger.Info("Stop command triggered by user.");

        // First, stop any active navigation
        _webViewService.StopNavigation();

        // Then try to inject the /stop command into the OpenClaw UI
        var result = await _webViewService.InjectStopCommandAsync();
        if (!result)
        {
            App.Logger.Warning("Stop command injection did not find a target input.");
        }
    }


    private async Task OnClearSessionAsync()
    {
        await _webViewService.ClearBrowsingDataAsync();
    }

    private void OnRetry()
    {
        IsErrorVisible = false;
        _webViewService.RetryNavigation();
    }

    private void OnDevTools()
    {
        _webViewService.OpenDevTools();
    }

    private async Task OnQuickCommandAsync(string command)
    {
        App.Logger.Info($"Quick command: {command}");
        var result = await _webViewService.InjectQuickCommandAsync(command);
        if (!result)
        {
            App.Logger.Warning($"Quick command '{command}' injection did not find a target input.");
        }
    }

    /// <summary>
    /// Dismisses the error InfoBar.
    /// </summary>
    public void DismissError()
    {
        IsErrorVisible = false;
    }

    // --- Phase 4: Diagnostics ---

    private async Task OnRunDiagnosticsAsync()
    {
        App.Logger.Info("Running diagnostics...");
        var gatewayUrl = _selectedEnvironment?.GatewayUrl;
        var report = await DiagnosticService.RunAllAsync(gatewayUrl, _webViewService);
        DiagnosticSummary = report.ToSummary();
        IsDiagnosticVisible = true;
        App.Logger.Info($"Diagnostics complete. Failures: {report.HasFailures}");
    }

    public void DismissDiagnostics()
    {
        IsDiagnosticVisible = false;
    }

    private void OnConnectionStateChanged(ConnectionState state)
    {
        // Update on the UI thread
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ConnectionState = state;
            IsLoading = state == ConnectionState.Loading;

            (StatusMessage, StatusIcon) = state switch
            {
                ConnectionState.Connected => (StringResources.StatusConnected, "\uE701"), // Checkmark
                ConnectionState.Loading => (StringResources.StatusLoading, "\uE895"), // Sync
                ConnectionState.Reconnecting => (StringResources.StatusReconnecting, "\uE72C"), // Refresh
                ConnectionState.AuthFailed => (StringResources.StatusAuthFailed, "\uE72E"), // Shield error
                ConnectionState.Error => (StringResources.StatusError, "\uEA39"), // Error badge
                ConnectionState.Offline => (StringResources.StatusOffline, "\uE871"), // Globe
                _ => (StringResources.StatusOffline, "\uE871"),
            };

            // Show/hide error InfoBar for error states
            if (state is ConnectionState.Error or ConnectionState.AuthFailed or ConnectionState.Reconnecting)
            {
                ShowRetryButton = state is not ConnectionState.Reconnecting; // auto-retry handles reconnecting
            }
            else
            {
                IsErrorVisible = false;
            }
        });
    }

    private void OnNavigationError(string message)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ErrorMessage = message;
            IsErrorVisible = true;
            ErrorOccurred?.Invoke(message);
        });
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// A simple ICommand implementation for binding.
/// </summary>
public class SimpleCommand : ICommand
{
    private readonly Action _action;

    public SimpleCommand(Action action) => _action = action;


#pragma warning disable CS0067 // Required by ICommand interface
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _action();
}
