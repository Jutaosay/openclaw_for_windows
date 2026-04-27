// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml.Media;
using OpenClaw.Models;

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    public string DiagnosticSummary
    {
        get => _diagnosticSummary;
        private set => SetProperty(ref _diagnosticSummary, value);
    }

    public bool IsDiagnosticVisible
    {
        get => _isDiagnosticVisible;
        private set => SetProperty(ref _isDiagnosticVisible, value);
    }

    public string HeartbeatSummary
    {
        get => _heartbeatSummary;
        private set => SetProperty(ref _heartbeatSummary, value);
    }

    public Brush HeartbeatSummaryBrush
    {
        get => _heartbeatSummaryBrush;
        private set => SetProperty(ref _heartbeatSummaryBrush, value);
    }

    public string ModelSummaryText
    {
        get => _modelSummaryText;
        private set => SetProperty(ref _modelSummaryText, value);
    }

    public string AccessSummaryText
    {
        get => _accessSummaryText;
        private set => SetProperty(ref _accessSummaryText, value);
    }

    public Brush AccessSummaryBrush
    {
        get => _accessSummaryBrush;
        private set => SetProperty(ref _accessSummaryBrush, value);
    }

    public string LatencySummaryText
    {
        get => _latencySummaryText;
        private set => SetProperty(ref _latencySummaryText, value);
    }

    public Brush LatencySummaryBrush
    {
        get => _latencySummaryBrush;
        private set => SetProperty(ref _latencySummaryBrush, value);
    }

    public string WorkStatusText
    {
        get => _workStatusText;
        private set => SetProperty(ref _workStatusText, value);
    }

    public Brush WorkStatusBrush
    {
        get => _workStatusBrush;
        private set => SetProperty(ref _workStatusBrush, value);
    }

    public bool IsRunIndicatorsAnimating
    {
        get => _isRunIndicatorsAnimating;
        private set => SetProperty(ref _isRunIndicatorsAnimating, value);
    }

    /// <summary>
    /// Gets the current shell connection state.
    /// </summary>
    public RecoveryState ShellConnectionState
    {
        get => _shellConnectionState;
        private set => SetProperty(ref _shellConnectionState, value);
    }

    /// <summary>
    /// Gets whether recovery is in progress.
    /// </summary>
    public bool IsRecovering
    {
        get => _isRecovering;
        private set => SetProperty(ref _isRecovering, value);
    }

    /// <summary>
    /// Gets the current recovery message.
    /// </summary>
    public string RecoveryMessage
    {
        get => _recoveryMessage;
        private set => SetProperty(ref _recoveryMessage, value);
    }
}
