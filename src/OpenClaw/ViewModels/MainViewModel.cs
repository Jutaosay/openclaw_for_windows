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
public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private const int HeartbeatIndicatorCount = 12;
    private const int RunIndicatorCount = 12;
    private static readonly Brush NeutralBrush = CreateBrush(107, 114, 128);
    private static readonly Brush SuccessBrush = CreateBrush(34, 197, 94);
    private static readonly Brush WarningBrush = CreateBrush(245, 158, 11);
    private static readonly Brush ErrorBrush = CreateBrush(239, 68, 68);
    private readonly WebViewService _webViewService = new();
    private readonly HostedUiBridge _hostedUiBridge = new();
    private ShellSessionCoordinator? _coordinator;
    private EnvironmentConfig? _selectedEnvironment;
    private string _statusMessage = StringResources.StatusOffline;
    private Brush _statusIndicatorBrush = NeutralBrush;
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
    private HeartbeatProbeStatus? _lastHeartbeatStatus;

    // Recovery state projection
    private RecoveryState _shellConnectionState = RecoveryState.Connecting;
    private bool _isRecovering;
    private string _recoveryMessage = string.Empty;

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

        // Wire up WebViewService events (infrastructure only)
        _webViewService.ConnectionStateChanged += OnConnectionStateChanged;
        _webViewService.NavigationErrorOccurred += OnNavigationError;
        _webViewService.NavigationCompleted += OnNavigationCompleted;
        _webViewService.ControlUiSnapshotUpdated += OnControlUiSnapshotUpdated;
        _webViewService.HeartbeatObserved += OnHeartbeatObserved;

        // Wire up coordinator events
        InitializeCoordinator();

        LoadEnvironments();
    }

    private void InitializeCoordinator()
    {
        _coordinator = new ShellSessionCoordinator();
        _coordinator.RecoveryStateChanged += OnRecoveryStateChanged;
        _coordinator.HealthSnapshotUpdated += OnHealthSnapshotUpdated;
        _coordinator.TelemetryUpdated += OnTelemetryUpdated;
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

    public Brush StatusIndicatorBrush
    {
        get => _statusIndicatorBrush;
        private set
        {
            _statusIndicatorBrush = value;
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
    /// Gets the hosted UI bridge.
    /// </summary>
    public HostedUiBridge HostedUiBridge => _hostedUiBridge;

    /// <summary>
    /// Gets the shell session coordinator.
    /// </summary>
    public ShellSessionCoordinator? Coordinator => _coordinator;

    /// <summary>
    /// Gets the current shell connection state.
    /// </summary>
    public RecoveryState ShellConnectionState
    {
        get => _shellConnectionState;
        private set
        {
            _shellConnectionState = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets whether recovery is in progress.
    /// </summary>
    public bool IsRecovering
    {
        get => _isRecovering;
        private set
        {
            _isRecovering = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets the current recovery message.
    /// </summary>
    public string RecoveryMessage
    {
        get => _recoveryMessage;
        private set
        {
            _recoveryMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Initializes the WebView2 control. Called from the view after the control is loaded.
    /// </summary>
    public async Task InitializeWebViewAsync(WebView2 webView)
    {
        if (_selectedEnvironment is null || _coordinator is null)
        {
            return;
        }

        // Initialize WebViewService (infrastructure)
        await _webViewService.InitializeAsync(webView, _selectedEnvironment.Name);

        // Initialize HostedUiBridge
        await _hostedUiBridge.InitializeAsync(webView);

        // Attach coordinator
        await _coordinator.AttachAsync(_webViewService, _hostedUiBridge);
        _coordinator.SetEnvironment(_selectedEnvironment.Name, _selectedEnvironment.GatewayUrl);
        UpdateStatusPresentation();

        // Navigate
        if (_webViewService.IsInitialized && _selectedEnvironment is not null)
        {
            _webViewService.Navigate(_selectedEnvironment.GatewayUrl);
        }
    }

    public void Dispose()
    {
        _webViewService.ConnectionStateChanged -= OnConnectionStateChanged;
        _webViewService.NavigationErrorOccurred -= OnNavigationError;
        _webViewService.NavigationCompleted -= OnNavigationCompleted;
        _webViewService.ControlUiSnapshotUpdated -= OnControlUiSnapshotUpdated;
        _webViewService.HeartbeatObserved -= OnHeartbeatObserved;

        if (_coordinator is not null)
        {
            _coordinator.RecoveryStateChanged -= OnRecoveryStateChanged;
            _coordinator.HealthSnapshotUpdated -= OnHealthSnapshotUpdated;
            _coordinator.TelemetryUpdated -= OnTelemetryUpdated;
            _coordinator.Dispose();
            _coordinator = null;
        }

        _hostedUiBridge.Dispose();
        _webViewService.Dispose();
    }

    /// <summary>
    /// Reloads environments from configuration (e.g. after settings dialog closes).
    /// </summary>
    public void RefreshEnvironments()
    {
        LoadEnvironments();
    }

    /// <summary>
    /// Notifies the coordinator that the host window went to background.
    /// </summary>
    public void NotifyHostHidden()
    {
        _coordinator?.OnHostHidden();
    }

    /// <summary>
    /// Notifies the coordinator that the host window returned to foreground.
    /// </summary>
    public async Task NotifyHostVisibleAsync()
    {
        if (_coordinator is not null)
        {
            await _coordinator.OnHostVisibleAsync();
        }
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
        _coordinator?.Reset();
        _coordinator?.SetEnvironment(_selectedEnvironment.Name, _selectedEnvironment.GatewayUrl);
        UpdateStatusPresentation();
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
            ShowRetryButton = state is ConnectionState.Error or ConnectionState.AuthFailed;

            if (state is not ConnectionState.Connected && _selectedEnvironment is not null)
            {
                _webViewService.StopHeartbeat();
            }

            UpdateStatusPresentation();
        });
    }

    private void OnNavigationError(string message)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ErrorMessage = message;
            IsErrorVisible = true;
            ErrorOccurred?.Invoke(message);
            UpdateStatusPresentation();
        });
    }

    private void OnNavigationCompleted(string? uri)
    {
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

            if (snapshot.IsIssue && ConnectionState is ConnectionState.Error or ConnectionState.AuthFailed or ConnectionState.Reconnecting)
            {
                ErrorMessage = snapshot.DetailOrSummary;
            }
            else if (ConnectionState is not ConnectionState.Error and not ConnectionState.AuthFailed)
            {
                IsErrorVisible = false;
            }

            if (snapshot.Phase == ControlUiPhase.Connected && _selectedEnvironment is not null)
            {
                if (HeartbeatSummary == "HB --")
                {
                    HeartbeatSummary = StringResources.HeartbeatWait;
                    HeartbeatSummaryBrush = WarningBrush;
                }

                var interval = App.Configuration.Settings.Heartbeat.EnableHeartbeat
                    ? App.Configuration.Settings.Heartbeat.IntervalSeconds
                    : 0;
                _webViewService.StartHeartbeat(_selectedEnvironment.GatewayUrl, interval);
            }

            UpdateStatusPresentation();
        });
    }

    private void OnRecoveryStateChanged(RecoveryState state)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ShellConnectionState = state;
            IsRecovering = state is RecoveryState.Reconnecting or RecoveryState.Resyncing or RecoveryState.Refreshing;
            RecoveryMessage = FormatRecoveryMessage(state);
            UpdateStatusPresentation();
        });
    }

    private void OnHealthSnapshotUpdated(ConnectionHealthSnapshot snapshot)
    {
        // Update UI health indicators from health snapshot
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            // Could update additional UI elements based on detailed health
        });
    }

    private void OnTelemetryUpdated(RecoveryTelemetrySnapshot snapshot)
    {
        // Log telemetry changes if verbose logging is enabled
        if (App.Configuration.Settings.Diagnostics.EnableVerboseRecoveryLogging)
        {
            App.Logger.Info("recovery.telemetry", new
            {
                snapshot.TotalReconnectAttempts,
                snapshot.TotalSoftResyncAttempts,
                snapshot.TotalHardRefreshAttempts,
                snapshot.RecentGapCount,
                snapshot.CurrentRecoveryState
            });
        }
    }

    private static string FormatRecoveryMessage(RecoveryState state)
    {
        return state switch
        {
            RecoveryState.Connecting => StringResources.RecoveryConnecting,
            RecoveryState.Reconnecting => StringResources.RecoveryReconnecting,
            RecoveryState.Resyncing => StringResources.RecoveryResyncing,
            RecoveryState.Refreshing => StringResources.RecoveryRefreshing,
            RecoveryState.Degraded => StringResources.RecoveryDegraded,
            RecoveryState.Failed => StringResources.RecoveryFailed,
            _ => string.Empty,
        };
    }

    private void UpdateHeartbeatIndicators(HeartbeatProbeStatus status)
    {
        if (HeartbeatIndicators.Count == 0)
        {
            return;
        }

        if (status != HeartbeatProbeStatus.Healthy)
        {
            ResetHeartbeatIndicatorsToWarning();
            _lastHeartbeatStatus = status;
            return;
        }

        if (_lastHeartbeatStatus != HeartbeatProbeStatus.Healthy)
        {
            ResetHeartbeatIndicatorsToWarning();
        }

        for (var index = 0; index < HeartbeatIndicators.Count - 1; index++)
        {
            HeartbeatIndicators[index].FillBrush = HeartbeatIndicators[index + 1].FillBrush;
            HeartbeatIndicators[index].FillOpacity = HeartbeatIndicators[index + 1].FillOpacity;
        }

        HeartbeatIndicators[^1].FillBrush = CreateIndicatorBrush(status);
        HeartbeatIndicators[^1].FillOpacity = 0.98;
        _lastHeartbeatStatus = status;
    }

    private void ResetHeartbeatIndicatorsToWarning()
    {
        for (var index = 0; index < HeartbeatIndicators.Count; index++)
        {
            HeartbeatIndicators[index].FillBrush = CreateIndicatorBrush(HeartbeatProbeStatus.Connecting);
            HeartbeatIndicators[index].FillOpacity = CreateIndicatorOpacity(index);
        }
    }

    private static Brush CreateIndicatorBrush(HeartbeatProbeStatus status)
    {
        return status switch
        {
            HeartbeatProbeStatus.Healthy => SuccessBrush,
            _ => WarningBrush,
        };
    }

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue) =>
        new(Color.FromArgb(255, red, green, blue));

    private void UpdateStatusPresentation()
    {
        var presentation = CreateStatusPresentation();
        StatusMessage = presentation.Text;
        StatusIndicatorBrush = presentation.Brush;
    }

    private StatusPresentation CreateStatusPresentation()
    {
        return ShellConnectionState switch
        {
            RecoveryState.Reconnecting => new StatusPresentation(StringResources.RecoveryReconnecting, WarningBrush),
            RecoveryState.Resyncing => new StatusPresentation(StringResources.RecoveryResyncing, WarningBrush),
            RecoveryState.Refreshing => new StatusPresentation(StringResources.RecoveryRefreshing, WarningBrush),
            RecoveryState.Degraded when !string.IsNullOrWhiteSpace(RecoveryMessage) => new StatusPresentation(RecoveryMessage, WarningBrush),
            RecoveryState.Failed => new StatusPresentation(StringResources.RecoveryFailed, ErrorBrush),
            _ => CreateConnectionStatusPresentation(ConnectionState),
        };
    }

    private static StatusPresentation CreateConnectionStatusPresentation(ConnectionState state)
    {
        return state switch
        {
            ConnectionState.Connected => new StatusPresentation(StringResources.StatusConnected, SuccessBrush),
            ConnectionState.Loading => new StatusPresentation(StringResources.StatusLoading, WarningBrush),
            ConnectionState.GatewayConnecting => new StatusPresentation(StringResources.StatusGatewayConnecting, WarningBrush),
            ConnectionState.Reconnecting => new StatusPresentation(StringResources.StatusReconnecting, WarningBrush),
            ConnectionState.AuthFailed => new StatusPresentation(StringResources.StatusAuthFailed, ErrorBrush),
            ConnectionState.Error => new StatusPresentation(StringResources.StatusError, ErrorBrush),
            _ => new StatusPresentation(StringResources.StatusOffline, NeutralBrush),
        };
    }

    private void OnHeartbeatObserved(HeartbeatProbeResult result)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            (HeartbeatSummary, HeartbeatSummaryBrush) = FormatHeartbeatSummary(result.Status);
            UpdateHeartbeatIndicators(result.Status);
            UpdateStatusPresentation();
        });
    }

    private static (string Text, Brush Brush) FormatHeartbeatSummary(HeartbeatProbeStatus status)
    {
        return status switch
        {
            HeartbeatProbeStatus.Healthy => (StringResources.HeartbeatOk, SuccessBrush),
            HeartbeatProbeStatus.Connecting => (StringResources.HeartbeatWait, WarningBrush),
            HeartbeatProbeStatus.SessionBlocked => (StringResources.HeartbeatBlocked, WarningBrush),
            HeartbeatProbeStatus.Failure => (StringResources.HeartbeatFailed, WarningBrush),
            _ => ("HB --", WarningBrush),
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
            ControlUiPhase.AuthRequired => ("AUTH LOGIN", WarningBrush),
            ControlUiPhase.PairingRequired => ("AUTH PAIR", WarningBrush),
            ControlUiPhase.OriginRejected => ("AUTH ORIGIN", WarningBrush),
            ControlUiPhase.GatewayConnecting or ControlUiPhase.PageLoaded or ControlUiPhase.Loading => ("AUTH WAIT", WarningBrush),
            _ => ("AUTH --", WarningBrush),
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
            return ("IDLE", WarningBrush, RunIndicatorMode.Idle);
        }

        return ("WAIT", WarningBrush, RunIndicatorMode.Wait);
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
            RunIndicatorMode.Live => SuccessBrush,
            _ => WarningBrush,
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
        HeartbeatSummary = StringResources.HeartbeatWait;
        HeartbeatSummaryBrush = WarningBrush;
        StatusIndicatorBrush = NeutralBrush;
        ModelSummaryText = "--";
        AccessSummaryText = "AUTH --";
        AccessSummaryBrush = WarningBrush;
        WorkStatusText = "WAIT";
        WorkStatusBrush = WarningBrush;
        SetRunIndicatorMode(RunIndicatorMode.Wait);
        _lastHeartbeatStatus = null;
        ResetHeartbeatIndicatorsToWarning();
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

internal readonly record struct StatusPresentation(string Text, Brush Brush);

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
