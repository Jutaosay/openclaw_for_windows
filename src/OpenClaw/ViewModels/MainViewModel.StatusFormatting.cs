// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml.Media;
using OpenClaw.Helpers;
using OpenClaw.Models;
using OpenClaw.Services;

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
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

    private readonly record struct StatusPresentation(string Text, Brush Brush);
}
