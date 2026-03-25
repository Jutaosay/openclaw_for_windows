// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.UI.Xaml;
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
        ClearSessionCommand = new SimpleCommand(async () => await OnClearSessionAsync());
        OpenSettingsCommand = new SimpleCommand(() => OpenSettingsRequested?.Invoke());
        ReloadCommand = new SimpleCommand(OnReload);
        StopCommand = new SimpleCommand(OnStop);
        RetryCommand = new SimpleCommand(OnRetry);
        DevToolsCommand = new SimpleCommand(OnDevTools);
        RunDiagnosticsCommand = new SimpleCommand(async () => await OnRunDiagnosticsAsync());
        ViewLogsCommand = new SimpleCommand(() => ViewLogsRequested?.Invoke());

        _webViewService.ConnectionStateChanged += OnConnectionStateChanged;
        _webViewService.NavigationErrorOccurred += OnNavigationError;
        _webViewService.HeartbeatFailed += OnHeartbeatFailed;

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
                OnPropertyChanged(nameof(SelectedEnvironmentName));
                OnEnvironmentChanged();
            }
        }
    }

    public string CurrentUrl => _selectedEnvironment?.GatewayUrl ?? string.Empty;

    public string SelectedEnvironmentName => _selectedEnvironment?.Name ?? string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string StatusIcon
    {
        get => _statusIcon;
        private set
        {
            _statusIcon = value;
            OnPropertyChanged();
        }
    }

    public ConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            _connectionState = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LoadingVisibility));
        }
    }

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsErrorVisible
    {
        get => _isErrorVisible;
        private set
        {
            _isErrorVisible = value;
            OnPropertyChanged();
        }
    }

    public bool ShowRetryButton
    {
        get => _showRetryButton;
        private set
        {
            _showRetryButton = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RetryButtonVisibility));
        }
    }

    public Visibility RetryButtonVisibility => ShowRetryButton ? Visibility.Visible : Visibility.Collapsed;

    public string DiagnosticSummary
    {
        get => _diagnosticSummary;
        private set
        {
            _diagnosticSummary = value;
            OnPropertyChanged();
        }
    }

    public bool IsDiagnosticVisible
    {
        get => _isDiagnosticVisible;
        private set
        {
            _isDiagnosticVisible = value;
            OnPropertyChanged();
        }
    }

    public ICommand ClearSessionCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand DevToolsCommand { get; }
    public ICommand RunDiagnosticsCommand { get; }
    public ICommand ViewLogsCommand { get; }

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
    /// Reloads environments from configuration (e.g. after settings dialog closes).
    /// </summary>
    public void RefreshEnvironments()
    {
        LoadEnvironments();
    }

    private void LoadEnvironments()
    {
        var settings = App.Configuration.Settings;
        Environments.Clear();

        foreach (var env in settings.Environments)
        {
            Environments.Add(env);
        }

        _selectedEnvironment = App.Configuration.GetSelectedEnvironment();
        OnPropertyChanged(nameof(SelectedEnvironment));
        OnPropertyChanged(nameof(CurrentUrl));
        OnPropertyChanged(nameof(SelectedEnvironmentName));
    }

    private void OnEnvironmentChanged()
    {
        if (_selectedEnvironment is null)
        {
            return;
        }

        App.Configuration.Settings.SelectedEnvironmentName = _selectedEnvironment.Name;
        App.Configuration.Save();

        if (_webViewService.IsInitialized)
        {
            _webViewService.Navigate(_selectedEnvironment.GatewayUrl);
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

    private void OnReload()
    {
        _webViewService.Reload();
    }

    private void OnStop()
    {
        _webViewService.Stop();
    }

    private void OnDevTools()
    {
        _webViewService.OpenDevTools();
    }

    /// <summary>
    /// Dismisses the error InfoBar.
    /// </summary>
    public void DismissError()
    {
        IsErrorVisible = false;
    }

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
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ConnectionState = state;
            IsLoading = state == ConnectionState.Loading;

            (StatusMessage, StatusIcon) = state switch
            {
                ConnectionState.Connected => (StringResources.StatusConnected, "\uE701"),
                ConnectionState.Loading => (StringResources.StatusLoading, "\uE895"),
                ConnectionState.Reconnecting => (StringResources.StatusReconnecting, "\uE72C"),
                ConnectionState.AuthFailed => (StringResources.StatusAuthFailed, "\uE72E"),
                ConnectionState.Error => (StringResources.StatusError, "\uEA39"),
                ConnectionState.Offline => (StringResources.StatusOffline, "\uE871"),
                _ => (StringResources.StatusOffline, "\uE871"),
            };

            if (state is ConnectionState.Error or ConnectionState.AuthFailed or ConnectionState.Reconnecting)
            {
                ShowRetryButton = state is not ConnectionState.Reconnecting;
            }
            else
            {
                IsErrorVisible = false;
            }

            if (state == ConnectionState.Connected && _selectedEnvironment is not null)
            {
                var interval = App.Configuration.Settings.HeartbeatIntervalSeconds;
                _webViewService.StartHeartbeat(_selectedEnvironment.GatewayUrl, interval);
            }
            else
            {
                _webViewService.StopHeartbeat();
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

    private void OnHeartbeatFailed()
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            StatusMessage = StringResources.StatusHeartbeatFailed;
            StatusIcon = "\uE72C";
            ConnectionState = ConnectionState.Reconnecting;
            ErrorMessage = StringResources.StatusHeartbeatFailed;
            IsErrorVisible = true;
            ShowRetryButton = false;
            App.Logger.Warning("Heartbeat failure - auto-reconnecting.");
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

#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _action();
}
