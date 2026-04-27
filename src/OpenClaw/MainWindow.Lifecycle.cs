// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClaw.Helpers;

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

            if (width > 0 && height > 0)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            }

            if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
            {
                appWindow.Move(new Windows.Graphics.PointInt32((int)settings.WindowLeft, (int)settings.WindowTop));
            }
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Failed to restore window bounds: {ex.Message}");
        }
    }

    private void SaveWindowBounds()
    {
        try
        {
            var appWindow = this.AppWindow;
            var pos = appWindow.Position;
            var size = appWindow.Size;

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

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _runIndicatorTimer.Stop();
        _runIndicatorTimer.Tick -= OnRunIndicatorTick;
        _webViewRecreationTimer.Stop();
        _webViewRecreationTimer.Tick -= OnWebViewRecreationTimerTick;
        ViewModel.OpenSettingsRequested -= OnOpenSettingsRequested;
        ViewModel.WebViewRecreationRequested -= OnWebViewRecreationRequested;
        ViewModel.ViewLogsRequested -= OnViewLogsRequested;
        ViewModel.ErrorOccurred -= OnError;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Dispose();
        SaveWindowBounds();
        App.Configuration.FlushDeferredSave();
        App.Logger.Info("Application closing.");
        App.Logger.Dispose();
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
        WindowFrameHelper.QueueFrameRefresh(this, DispatcherQueue, RefreshTitleBarVisualState);
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
        }
    }

    private void OnRootActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateTitleBarColors(sender.ActualTheme);
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
