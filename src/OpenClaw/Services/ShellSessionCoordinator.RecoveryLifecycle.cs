// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public sealed partial class ShellSessionCoordinator
{
    private enum RecoveryOperationKind
    {
        Reconnect,
        SoftResync,
        HardRefresh,
    }

    private sealed class RecoveryOperationContext
    {
        public RecoveryOperationContext(
            RecoveryOperationKind kind,
            string reason,
            int attempt,
            CancellationTokenSource cancellationSource)
        {
            Kind = kind;
            Reason = reason;
            Attempt = attempt;
            CancellationSource = cancellationSource;
        }

        public RecoveryOperationKind Kind { get; }

        public string Reason { get; }

        public int Attempt { get; }

        public CancellationTokenSource CancellationSource { get; }

        public CancellationToken CancellationToken => CancellationSource.Token;
    }

    private RecoveryOperationContext? TryStartRecoveryOperation(RecoveryOperationKind operation, string reason)
    {
        RecoveryOperationContext context;

        lock (_recoveryGate)
        {
            if (ShouldThrottleRecoveryOperation(operation, reason))
            {
                return null;
            }

            var startedAt = DateTimeOffset.Now;
            var cancellationSource = PrepareRecoveryCancellationSource();
            var attempt = RegisterRecoveryOperationStart(operation, reason, startedAt);
            context = new RecoveryOperationContext(operation, reason, attempt, cancellationSource);
        }

        SetRecoveryState(GetRecoveryOperationState(context.Kind));
        LogRecoveryOperationStart(context.Kind, reason, context.Attempt);
        return context;
    }

    private CancellationTokenSource PrepareRecoveryCancellationSource()
    {
        _recoveryCts?.Cancel();
        _recoveryCts?.Dispose();
        _recoveryCts = new CancellationTokenSource();
        return _recoveryCts;
    }

    private int RegisterRecoveryOperationStart(RecoveryOperationKind operation, string reason, DateTimeOffset startedAt)
    {
        _isRecoveryInProgress = true;
        _lastRecoveryStartedAt = startedAt;
        _lastRecoveryReason = reason;

        return operation switch
        {
            RecoveryOperationKind.Reconnect => RegisterReconnectAttempt(),
            RecoveryOperationKind.SoftResync => RegisterSoftResyncAttempt(),
            RecoveryOperationKind.HardRefresh => RegisterHardRefreshAttempt(startedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private int RegisterReconnectAttempt()
    {
        _reconnectAttempts++;
        _totalReconnectAttempts++;
        return _reconnectAttempts;
    }

    private int RegisterSoftResyncAttempt()
    {
        _softResyncAttempts++;
        _totalSoftResyncAttempts++;
        return _softResyncAttempts;
    }

    private int RegisterHardRefreshAttempt(DateTimeOffset startedAt)
    {
        _hardRefreshAttempts++;
        _totalHardRefreshAttempts++;
        _lastHardRefreshAt = startedAt;
        return _hardRefreshAttempts;
    }

    private static RecoveryState GetRecoveryOperationState(RecoveryOperationKind operation)
    {
        return operation switch
        {
            RecoveryOperationKind.Reconnect => RecoveryState.Reconnecting,
            RecoveryOperationKind.SoftResync => RecoveryState.Resyncing,
            RecoveryOperationKind.HardRefresh => RecoveryState.Refreshing,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private void LogRecoveryOperationStart(RecoveryOperationKind operation, string reason, int attempt)
    {
        switch (operation)
        {
            case RecoveryOperationKind.Reconnect:
                _logger.Info("recovery.reconnect.start", new { reason, attempt });
                break;
            case RecoveryOperationKind.SoftResync:
                _logger.Info("recovery.soft_resync.start", new { reason, attempt });
                break;
            case RecoveryOperationKind.HardRefresh:
                _logger.Info("recovery.hard_refresh", new { reason, attempt });
                break;
        }
    }

    private bool ShouldThrottleRecoveryOperation(RecoveryOperationKind operation, string reason)
    {
        if (_isRecoveryInProgress)
        {
            _logger.Info("recovery.throttled", new
            {
                reason,
                operation = GetRecoveryOperationName(operation),
                current = _recoveryState
            });
            return true;
        }

        if (operation == RecoveryOperationKind.HardRefresh && ShouldThrottleHardRefresh())
        {
            _logger.Warning("recovery.hard_refresh.throttled", new { reason, lastRefreshAt = _lastHardRefreshAt });
            return true;
        }

        if (operation == RecoveryOperationKind.Reconnect &&
            _recoveryState == RecoveryState.Refreshing &&
            ShouldThrottleHardRefresh())
        {
            _logger.Warning("recovery.throttled", new
            {
                reason,
                operation = GetRecoveryOperationName(operation),
                lastRefreshAt = _lastHardRefreshAt
            });
            return true;
        }

        return false;
    }

    private static string GetRecoveryOperationName(RecoveryOperationKind operation)
    {
        return operation switch
        {
            RecoveryOperationKind.Reconnect => "reconnect",
            RecoveryOperationKind.SoftResync => "soft_resync",
            RecoveryOperationKind.HardRefresh => "hard_refresh",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private static void ThrowIfRecoveryCancelled(RecoveryOperationContext operation)
    {
        operation.CancellationToken.ThrowIfCancellationRequested();
    }

    private void CompleteRecoveryOperation(RecoveryOperationContext operation)
    {
        lock (_recoveryGate)
        {
            if (ReferenceEquals(_recoveryCts, operation.CancellationSource))
            {
                _isRecoveryInProgress = false;
                _recoveryCts = null;
            }
        }

        operation.CancellationSource.Dispose();
        PublishTelemetry();
    }

    private void AbortRecoveryOperation()
    {
        CancellationTokenSource? cancellationSource;

        lock (_recoveryGate)
        {
            cancellationSource = _recoveryCts;
            _recoveryCts = null;
            _isRecoveryInProgress = false;
        }

        cancellationSource?.Cancel();
        cancellationSource?.Dispose();
    }
}
