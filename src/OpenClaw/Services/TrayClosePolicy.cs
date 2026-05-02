// Copyright (c) Lanstack @openclaw. All rights reserved.

namespace OpenClaw.Services;

public sealed class TrayClosePolicy
{
    private bool _isExitRequested;

    public TrayCloseDisposition GetCloseDisposition(bool closeToTray) =>
        _isExitRequested || !closeToTray ? TrayCloseDisposition.Exit : TrayCloseDisposition.HideToTray;

    public void RequestExit()
    {
        _isExitRequested = true;
    }
}

public enum TrayCloseDisposition
{
    HideToTray,
    Exit,
}
