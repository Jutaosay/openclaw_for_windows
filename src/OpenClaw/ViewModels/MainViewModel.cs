// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Helpers;
using OpenClaw.Models;
using OpenClaw.Services;
using Windows.UI;

namespace OpenClaw.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Manages environment selection, WebView2 commands, and connection state.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private const int HeartbeatIndicatorCount = 12;
    private const int RunIndicatorCount = 12;
    private static readonly Brush NeutralBrush = CreateBrush(107, 114, 128);
    private static readonly Brush SuccessBrush = CreateBrush(34, 197, 94);
    private static readonly Brush WarningBrush = CreateBrush(245, 158, 11);
    private static readonly Brush ErrorBrush = CreateBrush(239, 68, 68);
    private readonly WebViewService _webViewService = new();
    private EnvironmentConfig? _selectedEnvironment;
    private string _statusMessage = StringResources.StatusOffline;
    private ConnectionState _connectionState = ConnectionState.Offline;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private bool _isErrorVisible;
    private bool _showRetryButton;
    private string _diagnosticSummary = string.Empty;
    private bool _isDiagnosticVisible;
    private string _heartbeatSummary = "HB --";
    private Brush _heartbeatSummaryBrush = NeutralBrush;
    private string _modelSummaryText = "--";
    private string _accessSummaryText = "AUTH --";
    private Brush _accessSummaryBrush = NeutralBrush;

    private string _workStatusText = "WAIT";
    private Brush _workStatusBrush = NeutralBrush;
    private RunIndicatorMode _runIndicatorMode = RunIndicatorMode.Wait;
    private bool _isRunIndicatorsAnimating;
    private int _runAnimationFrame;

    public MainViewModel()
    {
        for (var index = 0; index < HeartbeatIndicatorCount; index++)
        {
            HeartbeatIndicators.Add(new HeartbeatIndicatorViewModel
            {
                FillBrush = CreateIndicatorBrush(HeartbeatProbeStatus.Connecting),
                FillOpacity = CreateIndicatorOpacity(index),
            });
        }

        for (var index = 0; index < RunIndicatorCount; index++)
        {
            RunIndicators.Add(new HeartbeatIndicatorViewModel
            {
                FillBrush = CreateRunIndicatorBrush(RunIndicatorMode.Wait),
                FillOpacity = CreateRunIndicatorOpacity(RunIndicatorMode.Wait, index, 0),
            });
        }

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
        _webViewService.HeartbeatObserved += OnHeartbeatObserved;
        _webViewService.ControlUiSnapshotUpdated += OnControlUiSnapshotUpdated;

        LoadEnvironments();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when the user requests to open the settings dialog.
    /// </summary>
    public event Action? OpenSettingsRequested;

    /// <summary>
    /// Raised when the active environment requires the embedded WebView2 to be recreated.
    /// </summary>
    public event Action? WebViewRecreationRequested;

    /// <summary>
    /// Raised when the user requests to view logs.
    /// </summary>
    public event Action? ViewLogsRequested;

    /// <summary>
    /// Raised when a navigation error occurs, for display to the user.
    /// </summary>
    public event Action<string>? ErrorOccurred;

    public ObservableCollection<EnvironmentConfig> Environments { get; } = [];
    public ObservableCollection<HeartbeatIndicatorViewModel> HeartbeatIndicators { get; } = [];
    public ObservableCollection<HeartbeatIndicatorViewModel> RunIndicators { get; } = [];

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

    public string HeartbeatSummary
    {
        get => _heartbeatSummary;
        private set
        {
            _heartbeatSummary = value;
            OnPropertyChanged();
        }
    }

    public Brush HeartbeatSummaryBrush
    {
        get => _heartbeatSummaryBrush;
        private set
        {
            _heartbeatSummaryBrush = value;
            OnPropertyChanged();
        }
    }

    public string ModelSummaryText
    {
        get => _modelSummaryText;
        private set
        {
            _modelSummaryText = value;
            OnPropertyChanged();
        }
    }

    public string AccessSummaryText
    {
        get => _accessSummaryText;
        private set
        {
            _accessSummaryText = value;
            OnPropertyChanged();
        }
    }

    public Brush AccessSummaryBrush
    {
        get => _accessSummaryBrush;
        private set
        {
            _accessSummaryBrush = value;
            OnPropertyChanged();
        }
    }


    public string WorkStatusText
    {
        get => _workStatusText;
        private set
        {
            _workStatusText = value;
            OnPropertyChanged();
        }
    }

    public Brush WorkStatusBrush
    {
        get => _workStatusBrush;
        private set
        {
            _workStatusBrush = value;
            OnPropertyChanged();
        }
    }

    public bool IsRunIndicatorsAnimating
    {
        get => _isRunIndicatorsAnimating;
        private set
        {
            _isRunIndicatorsAnimating = value;
            OnPropertyChanged();
        }
    }

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
        if (_selectedEnvironment is null)
        {
            return;
        }

        await _webViewService.InitializeAsync(webView, _selectedEnvironment.Name);

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
        ResetTelemetry();
    }

    private void OnEnvironmentChanged()
    {
        if (_selectedEnvironment is null)
        {
            return;
        }

        ResetTelemetry();
        App.Configuration.Settings.SelectedEnvironmentName = _selectedEnvironment.Name;
        App.Configuration.Save();

        if (_webViewService.IsInitialized)
        {
            if (_webViewService.IsUsingEnvironmentProfile(_selectedEnvironment.Name))
            {
                _webViewService.Navigate(_selectedEnvironment.GatewayUrl);
            }
            else
            {
                WebViewRecreationRequested?.Invoke();
            }
        }
    }

    public async Task ClearSessionForEnvironmentAsync(string environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            return;
        }

        await _webViewService.ClearEnvironmentSessionAsync(environmentName);

        if (string.Equals(_selectedEnvironment?.Name, environmentName, StringComparison.Ordinal))
        {
            DismissError();
            DismissDiagnostics();
            WebViewRecreationRequested?.Invoke();
        }
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
            IsLoading = state is ConnectionState.Loading or ConnectionState.GatewayConnecting;
            StatusMessage = FormatStatusMessage(state);
            ShowRetryButton = state is ConnectionState.Error or ConnectionState.AuthFailed;

            if (state is ConnectionState.Error or ConnectionState.AuthFailed or ConnectionState.Reconnecting)
            {
                var snapshot = _webViewService.LatestControlUiSnapshot;
                if (snapshot.IsIssue)
                {
                    ErrorMessage = snapshot.DetailOrSummary;
                }
            }
            else
            {
                IsErrorVisible = false;
            }

            if (state == ConnectionState.Connected && _selectedEnvironment is not null)
            {
                HeartbeatSummary = "HB OK";
                HeartbeatSummaryBrush = SuccessBrush;
                ShiftHeartbeatIndicators(HeartbeatProbeStatus.Healthy);

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

    private void OnHeartbeatObserved(HeartbeatProbeResult result)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ShiftHeartbeatIndicators(result.Status);
            (HeartbeatSummary, HeartbeatSummaryBrush) = FormatHeartbeatSummary(result.Status);
        });
    }

    private void OnControlUiSnapshotUpdated(ControlUiProbeSnapshot snapshot)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ModelSummaryText = FormatModelSummary(snapshot.CurrentModel);
            (AccessSummaryText, AccessSummaryBrush) = FormatAccessSummary(snapshot);

            var (workStatusText, workStatusBrush, runIndicatorMode) = FormatWorkStatus(snapshot);
            WorkStatusText = workStatusText;
            WorkStatusBrush = workStatusBrush;
            SetRunIndicatorMode(runIndicatorMode);
        });
    }

    private void OnHeartbeatFailed(string message)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            StatusMessage = StringResources.StatusHeartbeatFailed;
            ConnectionState = ConnectionState.Reconnecting;
            ErrorMessage = string.IsNullOrWhiteSpace(message)
                ? StringResources.StatusHeartbeatFailed
                : message;
            IsErrorVisible = true;
            ShowRetryButton = false;
            HeartbeatSummary = "HB RETRY";
            HeartbeatSummaryBrush = ErrorBrush;
            App.Logger.Warning($"Heartbeat failure - refreshing hosted UI. Reason: {ErrorMessage}");
        });
    }

    private void ShiftHeartbeatIndicators(HeartbeatProbeStatus status)
    {
        if (HeartbeatIndicators.Count == 0)
        {
            return;
        }

        for (var index = 0; index < HeartbeatIndicators.Count - 1; index++)
        {
            HeartbeatIndicators[index].FillBrush = HeartbeatIndicators[index + 1].FillBrush;
            HeartbeatIndicators[index].FillOpacity = HeartbeatIndicators[index + 1].FillOpacity;
        }

        HeartbeatIndicators[^1].FillBrush = CreateIndicatorBrush(status);
        HeartbeatIndicators[^1].FillOpacity = 0.98;
    }

    private static Brush CreateIndicatorBrush(HeartbeatProbeStatus status)
    {
        return status switch
        {
            HeartbeatProbeStatus.Healthy => SuccessBrush,
            HeartbeatProbeStatus.Connecting => WarningBrush,
            HeartbeatProbeStatus.SessionBlocked or HeartbeatProbeStatus.Failure => ErrorBrush,
            _ => NeutralBrush,
        };
    }

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue) =>
        new(Color.FromArgb(255, red, green, blue));

    private static string FormatStatusMessage(ConnectionState state)
    {
        return state switch
        {
            ConnectionState.Connected => StringResources.StatusConnected,
            ConnectionState.Loading => StringResources.StatusLoading,
            ConnectionState.GatewayConnecting => StringResources.StatusGatewayConnecting,
            ConnectionState.Reconnecting => StringResources.StatusReconnecting,
            ConnectionState.AuthFailed => StringResources.StatusAuthFailed,
            ConnectionState.Error => StringResources.StatusError,
            _ => StringResources.StatusOffline,
        };
    }

    private static (string Text, Brush Brush) FormatHeartbeatSummary(HeartbeatProbeStatus status)
    {
        return status switch
        {
            HeartbeatProbeStatus.Healthy => ("HB OK", SuccessBrush),
            HeartbeatProbeStatus.Connecting => ("HB WAIT", WarningBrush),
            HeartbeatProbeStatus.SessionBlocked => ("HB BLOCK", ErrorBrush),
            HeartbeatProbeStatus.Failure => ("HB FAIL", ErrorBrush),
            _ => ("HB --", NeutralBrush),
        };
    }

    private static string FormatModelSummary(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return "--";
        }

        return model.Trim();
    }

    private static (string Text, Brush Brush) FormatAccessSummary(ControlUiProbeSnapshot snapshot)
    {
        return snapshot.Phase switch
        {
            ControlUiPhase.Connected => ("AUTH OK", SuccessBrush),
            ControlUiPhase.AuthRequired => ("AUTH LOGIN", ErrorBrush),
            ControlUiPhase.PairingRequired => ("AUTH PAIR", ErrorBrush),
            ControlUiPhase.OriginRejected => ("AUTH ORIGIN", ErrorBrush),
            ControlUiPhase.GatewayConnecting or ControlUiPhase.PageLoaded or ControlUiPhase.Loading => ("AUTH WAIT", WarningBrush),
            _ => ("AUTH --", NeutralBrush),
        };
    }


    private static (string Text, Brush Brush, RunIndicatorMode Mode) FormatWorkStatus(ControlUiProbeSnapshot snapshot)
    {
        if (snapshot.IsBusy || string.Equals(snapshot.WorkState, "busy", StringComparison.OrdinalIgnoreCase))
        {
            return ("LIVE", SuccessBrush, RunIndicatorMode.Live);
        }

        if (string.Equals(snapshot.WorkState, "idle", StringComparison.OrdinalIgnoreCase) ||
            snapshot.Phase == ControlUiPhase.Connected)
        {
            return ("IDLE", SuccessBrush, RunIndicatorMode.Idle);
        }

        return ("WAIT", NeutralBrush, RunIndicatorMode.Wait);
    }

    public void AdvanceRunIndicators()
    {
        if (_runIndicatorMode != RunIndicatorMode.Live)
        {
            return;
        }

        _runAnimationFrame = (_runAnimationFrame + 1) % RunIndicatorCount;
        ApplyRunIndicators();
    }

    private void SetRunIndicatorMode(RunIndicatorMode mode)
    {
        if (_runIndicatorMode == mode)
        {
            if (mode != RunIndicatorMode.Live)
            {
                ApplyRunIndicators();
            }

            return;
        }

        _runIndicatorMode = mode;
        _runAnimationFrame = 0;
        IsRunIndicatorsAnimating = mode == RunIndicatorMode.Live;
        ApplyRunIndicators();
    }

    private void ApplyRunIndicators()
    {
        for (var index = 0; index < RunIndicators.Count; index++)
        {
            RunIndicators[index].FillBrush = CreateRunIndicatorBrush(_runIndicatorMode);
            RunIndicators[index].FillOpacity = CreateRunIndicatorOpacity(_runIndicatorMode, index, _runAnimationFrame);
        }
    }

    private static Brush CreateRunIndicatorBrush(RunIndicatorMode mode)
    {
        return mode switch
        {
            RunIndicatorMode.Idle or RunIndicatorMode.Live => SuccessBrush,
            _ => NeutralBrush,
        };
    }

    private static double CreateRunIndicatorOpacity(RunIndicatorMode mode, int index, int frame)
    {
        if (mode != RunIndicatorMode.Live)
        {
            var normalized = (index + 1d) / RunIndicatorCount;
            return 0.24 + (normalized * 0.7);
        }

        // Wave sweep: dots near the wave head are brightest, fading behind.
        var distance = (index - frame + RunIndicatorCount) % RunIndicatorCount;
        var factor = 1d - (distance / (double)RunIndicatorCount);
        return 0.18 + (factor * 0.78);
    }

    private void ResetTelemetry()
    {
        HeartbeatSummary = "HB --";
        HeartbeatSummaryBrush = NeutralBrush;
        ModelSummaryText = "--";
        AccessSummaryText = "AUTH --";
        AccessSummaryBrush = NeutralBrush;
        WorkStatusText = "WAIT";
        WorkStatusBrush = NeutralBrush;
        SetRunIndicatorMode(RunIndicatorMode.Wait);

        foreach (var indicator in HeartbeatIndicators)
        {
            indicator.FillBrush = CreateIndicatorBrush(HeartbeatProbeStatus.Connecting);
        }

        for (var index = 0; index < HeartbeatIndicators.Count; index++)
        {
            HeartbeatIndicators[index].FillOpacity = CreateIndicatorOpacity(index);
        }
    }

    private static double CreateIndicatorOpacity(int index)
    {
        var normalized = (index + 1d) / HeartbeatIndicatorCount;
        return 0.2 + (normalized * 0.65);
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public enum RunIndicatorMode
{
    Wait,
    Idle,
    Live,
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

public sealed class HeartbeatIndicatorViewModel : INotifyPropertyChanged
{
    private Brush _fillBrush = new SolidColorBrush(Color.FromArgb(255, 107, 114, 128));
    private double _fillOpacity = 0.32;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Brush FillBrush
    {
        get => _fillBrush;
        set
        {
            _fillBrush = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FillBrush)));
        }
    }

    public double FillOpacity
    {
        get => _fillOpacity;
        set
        {
            _fillOpacity = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FillOpacity)));
        }
    }
}
