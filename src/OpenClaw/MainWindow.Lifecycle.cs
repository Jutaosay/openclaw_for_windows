// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClaw.Helpers;
using OpenClaw.Services;
using Windows.Graphics;

namespace OpenClaw;

public sealed partial class MainWindow
{
    private void RestoreWindowBounds()
    {
        var settings = App.Configuration.Settings;
        var appWindow = this.AppWindow;

        try
        {
            var width = (int)settings.WindowWidth;
            var height = (int)settings.WindowHeight;

            if (WindowBoundsUtilities.HasPersistableSize(width, height))
            {
                appWindow.Resize(new SizeInt32(width, height));
            }

            if (!WindowBoundsUtilities.HasSavedPosition(settings.WindowLeft, settings.WindowTop))
            {
                return;
            }

            var left = (int)settings.WindowLeft;
            var top = (int)settings.WindowTop;
            var displayWorkAreas = GetDisplayWorkAreas();
            if (WindowBoundsUtilities.IsVisibleWithinAnyWorkArea(left, top, width, height, displayWorkAreas))
            {
                appWindow.Move(new PointInt32(left, top));
                return;
            }

            if (TryGetCurrentDisplayWorkArea(appWindow, out var currentWorkArea) &&
                WindowBoundsUtilities.TryCenterInWorkArea(width, height, currentWorkArea, out var centeredLeft, out var centeredTop))
            {
                appWindow.Move(new PointInt32(centeredLeft, centeredTop));
                App.Logger.Info("Saved window bounds were outside current displays; moved window to the current display.");
            }
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Failed to restore window bounds: {ex.Message}");
        }
    }

    private void SaveWindowBounds()
    {
        if (_isWindowHidden || WindowFrameHelper.IsWindowMinimized(this))
        {
            App.Logger.Info("Skipping window bounds save because the window is hidden or minimized.");
            return;
        }

        try
        {
            var appWindow = this.AppWindow;
            var pos = appWindow.Position;
            var size = appWindow.Size;

            if (!WindowBoundsUtilities.CanPersistWindowBounds(pos.X, pos.Y, size.Width, size.Height))
            {
                App.Logger.Warning($"Skipping invalid window bounds: x={pos.X}, y={pos.Y}, width={size.Width}, height={size.Height}");
                return;
            }

            App.Configuration.Settings.WindowWidth = size.Width;
            App.Configuration.Settings.WindowHeight = size.Height;
            App.Configuration.Settings.WindowLeft = pos.X;
            App.Configuration.Settings.WindowTop = pos.Y;
            App.Configuration.Save();
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Failed to save window bounds: {ex.Message}");
        }
    }

    private static IReadOnlyList<WindowWorkArea> GetDisplayWorkAreas()
    {
        try
        {
            return DisplayArea.FindAll()
                .Select(displayArea => ToWindowWorkArea(displayArea.WorkArea))
                .Where(workArea => workArea.Width > 0 && workArea.Height > 0)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static bool TryGetCurrentDisplayWorkArea(AppWindow appWindow, out WindowWorkArea workArea)
    {
        workArea = default;

        try
        {
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea is null)
            {
                return false;
            }

            workArea = ToWindowWorkArea(displayArea.WorkArea);
            return workArea.Width > 0 && workArea.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    private static WindowWorkArea ToWindowWorkArea(RectInt32 workArea) =>
        new(workArea.X, workArea.Y, workArea.Width, workArea.Height);

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        AppWindow.Closing -= OnAppWindowClosing;
        _runIndicatorTimer.Stop();
        _runIndicatorTimer.Tick -= OnRunIndicatorTick;
        _webViewRecreationTimer.Stop();
        _webViewRecreationTimer.Tick -= OnWebViewRecreationTimerTick;
        DisposeTrayIcon();
        ViewModel.OpenSettingsRequested -= OnOpenSettingsRequested;
        ViewModel.WebViewRecreationRequested -= OnWebViewRecreationRequested;
        ViewModel.ViewLogsRequested -= OnViewLogsRequested;
        ViewModel.ErrorOccurred -= OnError;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        CloseSettingsWindow();
        ViewModel.Dispose();
        SaveWindowBounds();
        App.Configuration.FlushDeferredSave();
        App.Logger.Info("Application closing.");
        App.Logger.Dispose();
    }

    private void OnAppWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_trayClosePolicy.GetCloseDisposition(App.Configuration.Settings.CloseToTray) != TrayCloseDisposition.HideToTray ||
            _trayIconService?.IsAvailable != true)
        {
            return;
        }

        args.Cancel = true;
        HideMainWindowToTray();
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        _isWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;

        if (this.Content is FrameworkElement rootElement)
        {
            UpdateTitleBarColors(rootElement.ActualTheme);
        }

        UpdateWindowVisibilityState();

        if (_hasPerformedInitialTitleBarRefresh || !_isWindowActive)
        {
            return;
        }

        _hasPerformedInitialTitleBarRefresh = true;
        WindowFrameHelper.QueueFrameRefresh(this, DispatcherQueue, RefreshTitleBarVisualState, redrawWindow: true);
    }

    private void UpdateWindowVisibilityState()
    {
        var isMinimized = WindowFrameHelper.IsWindowMinimized(this);
        if (_isWindowActive)
        {
            if (_isWindowHidden && !isMinimized)
            {
                _isWindowHidden = false;
                OnWindowVisibleAsync();
            }
        }
        else if (isMinimized && !_isWindowHidden)
        {
            if (App.Configuration.Settings.MinimizeToTray &&
                _trayIconService?.IsAvailable == true)
            {
                HideMainWindowToTray();
                return;
            }

            _isWindowHidden = true;
            OnWindowHidden();
        }
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme(App.Configuration.Settings.AppTheme);
        if (!_hasInitializedWebViewHost)
        {
            _hasInitializedWebViewHost = true;
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => ScheduleWebViewRecreation("initial_load"));
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => PrewarmSettingsWindow());
        }
    }

    private void OnRootActualThemeChanged(FrameworkElement sender, object args)
    {
        _isDarkThemeActive = WindowFrameHelper.ApplyActualTheme(
            this,
            sender.ActualTheme,
            _isDarkThemeActive,
            UpdateTitleBarColors,
            DispatcherQueue,
            RefreshTitleBarVisualState,
            redrawWindow: true,
            useSizeNudgeOnDarkTransition: true,
            includeTrailingRefresh: true);
    }

    private void OnWindowHidden()
    {
        UpdateRunIndicatorAnimationState();
        ViewModel.NotifyHostHidden();
    }

    private async void OnWindowVisibleAsync()
    {
        await ViewModel.NotifyHostVisibleAsync();
        UpdateRunIndicatorAnimationState();
    }
}
