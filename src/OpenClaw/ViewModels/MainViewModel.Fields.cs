// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml.Media;
using OpenClaw.Helpers;
using OpenClaw.Models;
using OpenClaw.Services;

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    private const int HeartbeatIndicatorCount = 12;
    private const int RunIndicatorCount = 12;
    private const string DefaultHeartbeatSummary = "HB --";
    private const string DefaultModelSummary = "--";
    private const string DefaultAccessSummary = "AUTH --";
    private const string DefaultLatencySummary = "-- ms";
    private const string DefaultWorkStatus = "WAIT";

    private static readonly Brush NeutralBrush = CreateBrush(107, 114, 128);
    private static readonly Brush SuccessBrush = CreateBrush(34, 197, 94);
    private static readonly Brush WarningBrush = CreateBrush(245, 158, 11);
    private static readonly Brush ErrorBrush = CreateBrush(239, 68, 68);

    private readonly WebViewService _webViewService = new();
    private readonly HostedUiBridge _hostedUiBridge = new();
    private readonly ControlUiLatencyService _latencyService = new();

    private ShellSessionCoordinator? _coordinator;
    private EnvironmentConfig? _selectedEnvironment;
    private string _statusMessage = string.Empty;
    private Brush _statusIndicatorBrush = NeutralBrush;
    private ConnectionState _connectionState = ConnectionState.Offline;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private bool _isErrorVisible;
    private bool _showRetryButton;
    private string _diagnosticSummary = string.Empty;
    private bool _isDiagnosticVisible;
    private string _heartbeatSummary = DefaultHeartbeatSummary;
    private Brush _heartbeatSummaryBrush = NeutralBrush;
    private string _modelSummaryText = DefaultModelSummary;
    private string _accessSummaryText = DefaultAccessSummary;
    private Brush _accessSummaryBrush = NeutralBrush;
    private string _latencySummaryText = DefaultLatencySummary;
    private Brush _latencySummaryBrush = NeutralBrush;
    private string _workStatusText = DefaultWorkStatus;
    private Brush _workStatusBrush = NeutralBrush;
    private RunIndicatorMode _runIndicatorMode = RunIndicatorMode.Wait;
    private bool _isRunIndicatorsAnimating;
    private int _runAnimationFrame;
    private HeartbeatProbeStatus? _lastHeartbeatStatus;
    private bool _isHostVisible = true;

    // Recovery state projection
    private RecoveryState _shellConnectionState = RecoveryState.Connecting;
    private bool _isRecovering;
    private string _recoveryMessage = string.Empty;
}
