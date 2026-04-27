// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Helpers;

namespace OpenClaw;

public sealed partial class MainWindow
{
    private void OnThemeSelectionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string selectedTheme)
        {
            return;
        }

        App.Configuration.Settings.AppTheme = selectedTheme;
        App.Configuration.SaveDeferred();
        ApplyTheme(selectedTheme);
        UpdateThemeSelector(selectedTheme);
    }

    private void UpdateThemeSelector(string themeMode)
    {
        foreach (var button in EnumerateThemeButtons())
        {
            UpdateThemeButtonState(button, themeMode);
        }
    }

    private IEnumerable<Button> EnumerateThemeButtons()
    {
        yield return SystemThemeButton;
        yield return LightThemeButton;
        yield return DarkThemeButton;
    }

    private static void UpdateThemeButtonState(Button button, string selectedThemeMode)
    {
        var isSelected = string.Equals(button.Tag as string, selectedThemeMode, StringComparison.Ordinal);
        button.Background = isSelected
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 240, 255))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        button.Foreground = isSelected
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 99, 235))
            : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    }

    private void ApplyTheme(string themeMode)
    {
        _isDarkThemeActive = WindowFrameHelper.ApplyWindowTheme(
            this,
            themeMode,
            _isDarkThemeActive,
            UpdateTitleBarColors,
            DispatcherQueue,
            RefreshTitleBarVisualState,
            useSizeNudgeOnDarkTransition: true,
            includeTrailingRefresh: true);
    }

    private void RefreshTitleBarVisualState()
    {
        UpdateTitleBarInsets();

        if (this.Content is FrameworkElement rootElement)
        {
            UpdateTitleBarColors(rootElement.ActualTheme);
            AppTitleBar.InvalidateMeasure();
            AppTitleBar.InvalidateArrange();
            rootElement.InvalidateMeasure();
            rootElement.InvalidateArrange();
            rootElement.UpdateLayout();
        }
    }

    private void UpdateTitleBarInsets()
    {
        var titleBar = AppWindow.TitleBar;
        LeftInsetColumn.Width = new GridLength(titleBar.LeftInset);
        RightInsetColumn.Width = new GridLength(titleBar.RightInset);
    }

    private void UpdateTitleBarColors(ElementTheme actualTheme)
    {
        var palette = WindowFrameHelper.CreateThemePalette(actualTheme);
        var inactiveBackground = palette.IsDark
            ? Windows.UI.Color.FromArgb(255, 40, 40, 40)
            : Windows.UI.Color.FromArgb(255, 248, 248, 248);
        var currentCaptionColor = _isWindowActive ? palette.BackgroundColor : inactiveBackground;
        var currentForeground = _isWindowActive ? palette.ForegroundColor : palette.InactiveForegroundColor;

        WindowFrameHelper.ApplyTitleBarColors(this, new WindowTitleBarColors
        {
            IsDark = palette.IsDark,
            ForegroundColor = palette.ForegroundColor,
            BackgroundColor = Colors.Transparent,
            InactiveForegroundColor = palette.InactiveForegroundColor,
            InactiveBackgroundColor = Colors.Transparent,
            ButtonForegroundColor = currentForeground,
            ButtonBackgroundColor = Colors.Transparent,
            ButtonInactiveForegroundColor = palette.InactiveForegroundColor,
            ButtonInactiveBackgroundColor = Colors.Transparent,
            ButtonHoverForegroundColor = palette.ForegroundColor,
            ButtonHoverBackgroundColor = palette.ButtonHoverBackgroundColor,
            ButtonPressedForegroundColor = palette.ForegroundColor,
            ButtonPressedBackgroundColor = palette.ButtonPressedBackgroundColor,
            NativeBackgroundColor = currentCaptionColor,
            NativeTextColor = currentForeground,
        });

        UpdateTitleBarContentState(currentForeground, _isWindowActive);
        TopEdgeCover.Background = new SolidColorBrush(currentCaptionColor);
    }

    private void UpdateTitleBarContentState(Windows.UI.Color foregroundColor, bool isWindowActive)
    {
        AppTitleText.Foreground = new SolidColorBrush(foregroundColor);
        AppIcon.Opacity = isWindowActive ? 1.0 : 0.72;
    }
}
