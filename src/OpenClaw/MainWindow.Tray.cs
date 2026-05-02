// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Helpers;
using OpenClaw.Services;

namespace OpenClaw;

public sealed partial class MainWindow
{
    private void InitializeTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "WindowIcon.ico");
        _trayIconService = new TrayIconService(iconPath, App.Logger);
        if (!_trayIconService.IsAvailable)
        {
            App.Logger.Warning("Tray icon service is unavailable.");
            return;
        }

        _trayIconService.ToggleVisibilityRequested += OnTrayToggleVisibilityRequested;
        _trayIconService.OpenRequested += OnTrayOpenRequested;
        _trayIconService.OpenSettingsRequested += OnTrayOpenSettingsRequested;
        _trayIconService.ExitRequested += OnTrayExitRequested;
        _trayIconService.UpdateStatus(ViewModel.WorkStatusText);
    }

    private void DisposeTrayIcon()
    {
        if (_trayIconService is null)
        {
            return;
        }

        _trayIconService.ToggleVisibilityRequested -= OnTrayToggleVisibilityRequested;
        _trayIconService.OpenRequested -= OnTrayOpenRequested;
        _trayIconService.OpenSettingsRequested -= OnTrayOpenSettingsRequested;
        _trayIconService.ExitRequested -= OnTrayExitRequested;
        _trayIconService.Dispose();
        _trayIconService = null;
    }

    private void HideMainWindowToTray()
    {
        if (_isWindowHidden)
        {
            return;
        }

        SaveWindowBounds();
        WindowFrameHelper.HideWindow(this);
        _isWindowHidden = true;
        OnWindowHidden();
        App.Logger.Info("Main window hidden to tray.");
    }

    private void ShowMainWindowFromTray()
    {
        WindowFrameHelper.ShowAndActivateWindow(this);
        if (!_isWindowHidden)
        {
            return;
        }

        _isWindowHidden = false;
        OnWindowVisibleAsync();
        App.Logger.Info("Main window restored from tray.");
    }

    private void RequestApplicationExit()
    {
        _trayClosePolicy.RequestExit();
        Close();
    }

    private void UpdateTrayStatus()
    {
        _trayIconService?.UpdateStatus(ViewModel.WorkStatusText);
    }

    private void OnTrayToggleVisibilityRequested()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_isWindowHidden || WindowFrameHelper.IsWindowMinimized(this))
            {
                ShowMainWindowFromTray();
            }
            else
            {
                HideMainWindowToTray();
            }
        });
    }

    private void OnTrayOpenRequested()
    {
        DispatcherQueue.TryEnqueue(ShowMainWindowFromTray);
    }

    private void OnTrayOpenSettingsRequested()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ShowMainWindowFromTray();
            ShowSettingsWindow();
        });
    }

    private void OnTrayExitRequested()
    {
        DispatcherQueue.TryEnqueue(RequestApplicationExit);
    }
}
