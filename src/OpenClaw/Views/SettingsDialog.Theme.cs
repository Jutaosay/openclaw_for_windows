// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;
using OpenClaw.Helpers;

namespace OpenClaw.Views;

public sealed partial class SettingsDialog
{
    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme(App.Configuration.Settings.AppTheme);
        PopulateLanguageOptions();
        SetLanguageSelection(ViewModel.SelectedLanguage);
    }

    public void SyncWithCurrentSettings()
    {
        ApplyTheme(App.Configuration.Settings.AppTheme);
        PopulateLanguageOptions();
        SetLanguageSelection(ViewModel.SelectedLanguage);
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        SyncWithCurrentSettings();

        if (_hasPerformedInitialTitleBarRefresh)
        {
            return;
        }

        _hasPerformedInitialTitleBarRefresh = true;
        WindowFrameHelper.QueueFrameRefresh(this, DispatcherQueue, RefreshTitleBarVisualState);
    }

    private void OnRootActualThemeChanged(FrameworkElement sender, object args)
    {
        _isDarkThemeActive = WindowFrameHelper.ApplyActualTheme(
            this,
            sender.ActualTheme,
            _isDarkThemeActive,
            UpdateTitleBarColors,
            DispatcherQueue,
            RefreshTitleBarVisualState);
    }

    private void ApplyTheme(string themeMode)
    {
        _isDarkThemeActive = WindowFrameHelper.ApplyWindowTheme(
            this,
            themeMode,
            _isDarkThemeActive,
            UpdateTitleBarColors,
            DispatcherQueue,
            RefreshTitleBarVisualState);
    }

    private void RefreshTitleBarVisualState()
    {
        if (this.Content is FrameworkElement rootElement)
        {
            UpdateTitleBarColors(rootElement.ActualTheme);
            rootElement.InvalidateMeasure();
            rootElement.InvalidateArrange();
        }
    }

    private void UpdateTitleBarColors(ElementTheme actualTheme)
    {
        var palette = WindowFrameHelper.CreateThemePalette(actualTheme);
        WindowFrameHelper.ApplyTitleBarColors(this, new WindowTitleBarColors
        {
            IsDark = palette.IsDark,
            ForegroundColor = palette.ForegroundColor,
            BackgroundColor = palette.BackgroundColor,
            InactiveForegroundColor = palette.InactiveForegroundColor,
            InactiveBackgroundColor = palette.BackgroundColor,
            ButtonForegroundColor = palette.ForegroundColor,
            ButtonBackgroundColor = palette.BackgroundColor,
            ButtonInactiveForegroundColor = palette.InactiveForegroundColor,
            ButtonInactiveBackgroundColor = palette.BackgroundColor,
            ButtonHoverForegroundColor = palette.ForegroundColor,
            ButtonHoverBackgroundColor = palette.ButtonHoverBackgroundColor,
            ButtonPressedForegroundColor = palette.ForegroundColor,
            ButtonPressedBackgroundColor = palette.ButtonPressedBackgroundColor,
            NativeBackgroundColor = palette.BackgroundColor,
            NativeBorderColor = palette.BackgroundColor,
            NativeTextColor = palette.ForegroundColor,
        });
    }
}
