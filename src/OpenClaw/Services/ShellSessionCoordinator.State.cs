// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.Services;

public sealed partial class ShellSessionCoordinator
{
    private static HealthStatus MapHostedUiHealth(ControlUiProbeSnapshot snapshot)
    {
        return snapshot.Phase switch
        {
            ControlUiPhase.Connected => HealthStatus.Healthy,
            ControlUiPhase.AuthRequired or ControlUiPhase.PairingRequired or ControlUiPhase.OriginRejected => HealthStatus.Unhealthy,
            ControlUiPhase.GatewayError => HealthStatus.Degraded,
            _ => HealthStatus.Degraded,
        };
    }

    private static HealthStatus MapSessionHealth(ControlUiProbeSnapshot snapshot)
    {
        return snapshot.Phase switch
        {
            ControlUiPhase.Connected => HealthStatus.Healthy,
            ControlUiPhase.GatewayConnecting or ControlUiPhase.PageLoaded => HealthStatus.Degraded,
            _ => HealthStatus.Unhealthy,
        };
    }

    private HealthStatus MapStreamHealth(ControlUiProbeSnapshot snapshot)
    {
        return snapshot.Phase switch
        {
            ControlUiPhase.Connected => HealthStatus.Healthy,
            ControlUiPhase.GatewayConnecting or ControlUiPhase.PageLoaded => HealthStatus.Degraded,
            _ => _streamHealth,
        };
    }

    private void ApplyConnectionHealth(ConnectionState state)
    {
        _transportHealth = state switch
        {
            ConnectionState.Connected => HealthStatus.Healthy,
            ConnectionState.Loading or ConnectionState.GatewayConnecting => HealthStatus.Degraded,
            ConnectionState.Reconnecting => HealthStatus.Degraded,
            ConnectionState.AuthFailed or ConnectionState.Error => HealthStatus.Unhealthy,
            _ => HealthStatus.Unknown,
        };
    }

    private void ApplyHeartbeatHealth(HeartbeatProbeResult result)
    {
        switch (result.Status)
        {
            case HeartbeatProbeStatus.Healthy:
                _transportHealth = HealthStatus.Healthy;
                break;
            case HeartbeatProbeStatus.SessionBlocked:
                _sessionHealth = HealthStatus.Unhealthy;
                break;
            case HeartbeatProbeStatus.Failure:
                _transportHealth = HealthStatus.Unhealthy;
                break;
            case HeartbeatProbeStatus.Connecting:
                _transportHealth = HealthStatus.Degraded;
                break;
        }
    }
}
