// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml.Media;
using OpenClaw.Helpers;
using OpenClaw.Models;
using OpenClaw.Services;

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    private static void RunOnUiThread(Action action)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() => action());
    }

    private void OnConnectionStateChanged(ConnectionState state)
    {
        RunOnUiThread(() => ApplyConnectionState(state));
    }

    private void OnNavigationError(string message)
    {
        RunOnUiThread(() => ApplyNavigationError(message));
    }

    private void OnControlUiSnapshotUpdated(ControlUiProbeSnapshot snapshot)
    {
        RunOnUiThread(() => ApplyControlUiSnapshot(snapshot));
    }

    private void OnRecoveryStateChanged(RecoveryState state)
    {
        RunOnUiThread(() => ApplyRecoveryState(state));
    }

    private void OnTelemetryUpdated(RecoveryTelemetrySnapshot snapshot)
    {
        if (!App.Configuration.Settings.Diagnostics.EnableVerboseRecoveryLogging)
        {
            return;
        }

        App.Logger.Info("recovery.telemetry", new
        {
            snapshot.TotalReconnectAttempts,
            snapshot.TotalSoftResyncAttempts,
            snapshot.TotalHardRefreshAttempts,
            snapshot.RecentGapCount,
            snapshot.CurrentRecoveryState
        });
    }

    private void ApplyConnectionState(ConnectionState state)
    {
        ConnectionState = state;
        IsLoading = state is ConnectionState.Loading or ConnectionState.GatewayConnecting;
        ShowRetryButton = state is ConnectionState.Error or ConnectionState.AuthFailed;

        RefreshResourceScheduling();
        UpdateStatusPresentation();
    }

    private void ApplyNavigationError(string message)
    {
        ErrorMessage = message;
        IsErrorVisible = true;
        ErrorOccurred?.Invoke(message);
        UpdateStatusPresentation();
    }

    private void ApplyControlUiSnapshot(ControlUiProbeSnapshot snapshot)
    {
        ModelSummaryText = FormatModelSummary(snapshot.CurrentModel);
        (AccessSummaryText, AccessSummaryBrush) = FormatAccessSummary(snapshot);

        ApplyWorkStatus(snapshot);
        ApplySnapshotErrorState(snapshot);
        StartHeartbeatIfReady(snapshot);

        UpdateStatusPresentation();
    }

    private void ApplyRecoveryState(RecoveryState state)
    {
        ShellConnectionState = state;
        IsRecovering = state is RecoveryState.Reconnecting or RecoveryState.Resyncing or RecoveryState.Refreshing;
        RecoveryMessage = FormatRecoveryMessage(state);
        UpdateStatusPresentation();
    }

    private void ApplyWorkStatus(ControlUiProbeSnapshot snapshot)
    {
        var (workStatusText, workStatusBrush, runIndicatorMode) = FormatWorkStatus(snapshot);
        WorkStatusText = workStatusText;
        WorkStatusBrush = workStatusBrush;
        SetRunIndicatorMode(runIndicatorMode);
    }

    private void ApplySnapshotErrorState(ControlUiProbeSnapshot snapshot)
    {
        if (snapshot.IsIssue && ConnectionState is ConnectionState.Error or ConnectionState.AuthFailed or ConnectionState.Reconnecting)
        {
            ErrorMessage = snapshot.DetailOrSummary;
        }
        else if (ConnectionState is not ConnectionState.Error and not ConnectionState.AuthFailed)
        {
            IsErrorVisible = false;
        }
    }

    private void StartHeartbeatIfReady(ControlUiProbeSnapshot snapshot)
    {
        RefreshResourceScheduling();
    }

}
