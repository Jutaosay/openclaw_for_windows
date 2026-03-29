// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Services;
using OpenClaw.ViewModels;
using OpenClaw.Views;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace OpenClaw;

/// <summary>
/// The main application window. Hosts the WebView2 control, top bar, and status bar.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int DwmWindowAttributeUseImmersiveDarkMode = 20;
    private const int DwmWindowAttributeBorderColor = 34;
    private const int DwmWindowAttributeCaptionColor = 35;
    private const int DwmWindowAttributeTextColor = 36;
    private const uint DwmColorNone = 0xFFFFFFFE;
    private const uint SetWindowPosNoMove = 0x0002;
    private const uint SetWindowPosNoSize = 0x0001;
    private const uint SetWindowPosNoZOrder = 0x0004;
    private const uint SetWindowPosNoActivate = 0x0010;
    private const uint SetWindowPosFrameChanged = 0x0020;

    private bool _hasPerformedInitialTitleBarRefresh;
    private bool _isDarkThemeActive;
    private bool _hasInitializedWebViewHost;
    private bool _isRecreatingWebView;
    private bool _isWindowActive = true;
    private bool _pendingWebViewRecreation;
    private readonly DispatcherQueueTimer _runIndicatorTimer;

    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        this.InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        UpdateTitleBarInsets();

        // Set title and system backdrop
        Title = "OpenClaw";
        AppWindow.SetIcon("Assets\\WindowIcon.ico");
        SystemBackdrop = new MicaBackdrop
        {
            Kind = MicaKind.BaseAlt,
        };

        // Restore window size/position
        RestoreWindowBounds();

        // Wire up events
        ViewModel.OpenSettingsRequested += OnOpenSettingsRequested;
        ViewModel.WebViewRecreationRequested += OnWebViewRecreationRequested;
        ViewModel.ViewLogsRequested += OnViewLogsRequested;
        ViewModel.ErrorOccurred += OnError;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        _runIndicatorTimer = DispatcherQueue.CreateTimer();
        _runIndicatorTimer.Interval = TimeSpan.FromMilliseconds(430);
        _runIndicatorTimer.Tick += OnRunIndicatorTick;

        // Save window bounds on close
        this.Closed += OnWindowClosed;
        this.Activated += OnWindowActivated;

        // Apply saved theme after content is loaded
        if (this.Content is FrameworkElement rootElement)
        {
            rootElement.Loaded += (s, e) =>
            {
                ApplyTheme(App.Configuration.Settings.AppTheme);
                if (!_hasInitializedWebViewHost)
                {
                    _hasInitializedWebViewHost = true;
                    _ = RecreateWebViewAsync();
                }
            };
            rootElement.ActualThemeChanged += (s, e) =>
            {
                UpdateTitleBarColors(rootElement.ActualTheme);
            };
        }

        // Set initial status dot color
        UpdateStatusDotColor(ConnectionState.Offline);
        UpdateThemeSelector(App.Configuration.Settings.AppTheme);
    }

    private void RestoreWindowBounds()
    {
        var settings = App.Configuration.Settings;

        // Get the AppWindow for positioning
        var appWindow = this.AppWindow;

        try
        {
            var width = (int)settings.WindowWidth;
            var height = (int)settings.WindowHeight;

            if (width > 0 && height > 0)
            {
                appWindow.Resize(new SizeInt32(width, height));
            }

            if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
            {
                appWindow.Move(new PointInt32((int)settings.WindowLeft, (int)settings.WindowTop));
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
        SaveWindowBounds();
        App.Logger.Info("Application closing.");
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        _isWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;

        if (this.Content is FrameworkElement rootElement)
        {
            UpdateTitleBarColors(rootElement.ActualTheme);
        }

        if (_hasPerformedInitialTitleBarRefresh || !_isWindowActive)
        {
            return;
        }

        _hasPerformedInitialTitleBarRefresh = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshTitleBarVisualState();
            ForceNonClientFrameRefresh();
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshTitleBarVisualState();
            });
        });
    }

    private SettingsDialog? _settingsWindow;

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        ShowSettingsWindow();
    }

    private void OnOpenSettingsRequested()
    {
        ShowSettingsWindow();
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsDialog
        {
            MainViewModel = this.ViewModel,
        };

        _settingsWindow.SettingsSaved += async () =>
        {
            ViewModel.RefreshEnvironments();
            ApplyTheme(App.Configuration.Settings.AppTheme);
            UpdateThemeSelector(App.Configuration.Settings.AppTheme);
            await RecreateWebViewAsync();
        };

        _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        _settingsWindow.Activate();
    }

    private void OnError(string message)
    {
        // Error is now displayed via the InfoBar binding
        App.Logger.Error($"UI error displayed: {message}");
    }

    private void OnWebViewRecreationRequested()
    {
        _ = RecreateWebViewAsync();
    }

    private void OnInfoBarClosed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel.DismissError();
    }

    private void OnDiagnosticInfoBarClosed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel.DismissDiagnostics();
    }

    private async void OnViewLogsRequested()
    {
        await ShowLogViewerAsync();
    }

    private async Task ShowLogViewerAsync()
    {
        var dialog = new LogViewerDialog
        {
            XamlRoot = this.Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }


    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ConnectionState))
        {
            UpdateStatusDotColor(ViewModel.ConnectionState);
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.IsRunIndicatorsAnimating))
        {
            UpdateRunIndicatorAnimationState();
        }
    }

    private void UpdateRunIndicatorAnimationState()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ViewModel.IsRunIndicatorsAnimating)
            {
                if (!_runIndicatorTimer.IsRunning)
                {
                    _runIndicatorTimer.Start();
                }
            }
            else
            {
                _runIndicatorTimer.Stop();
            }
        });
    }

    private void OnRunIndicatorTick(DispatcherQueueTimer sender, object args)
    {
        ViewModel.AdvanceRunIndicators();
    }

    private void UpdateStatusDotColor(ConnectionState state)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var brushKey = state switch
            {
                ConnectionState.Connected => "StatusConnectedBrush",
                ConnectionState.Loading or ConnectionState.GatewayConnecting or ConnectionState.Reconnecting => "StatusReconnectingBrush",
                ConnectionState.AuthFailed or ConnectionState.Error => "StatusErrorBrush",
                _ => "StatusOfflineBrush",
            };

            if (Application.Current.Resources.TryGetValue(brushKey, out var brush))
            {
                StatusDot.Fill = (SolidColorBrush)brush;
            }
        });
    }

    private async void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog
        {
            XamlRoot = this.Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async Task RecreateWebViewAsync()
    {
        if (_isRecreatingWebView)
        {
            _pendingWebViewRecreation = true;
            return;
        }

        _isRecreatingWebView = true;

        try
        {
            do
            {
                _pendingWebViewRecreation = false;

                var nextWebView = new WebView2();
                nextWebView.HorizontalAlignment = HorizontalAlignment.Stretch;
                nextWebView.VerticalAlignment = VerticalAlignment.Stretch;

                WebViewHost.Children.Clear();
                WebViewHost.Children.Add(nextWebView);

                await ViewModel.InitializeWebViewAsync(nextWebView);
            }
            while (_pendingWebViewRecreation);
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to recreate WebView2 host: {ex.Message}");
        }
        finally
        {
            _isRecreatingWebView = false;
        }
    }

    private void OnThemeSelectionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string selectedTheme)
        {
            return;
        }

        App.Configuration.Settings.AppTheme = selectedTheme;
        App.Configuration.Save();
        ApplyTheme(selectedTheme);
        UpdateThemeSelector(selectedTheme);
    }

    private void UpdateThemeSelector(string themeMode)
    {
        UpdateThemeButtonState(SystemThemeButton, themeMode == "System");
        UpdateThemeButtonState(LightThemeButton, themeMode == "Light");
        UpdateThemeButtonState(DarkThemeButton, themeMode == "Dark");
    }

    private static void UpdateThemeButtonState(Button button, bool isSelected)
    {
        button.Background = isSelected
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 240, 255))
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        button.Foreground = isSelected
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 99, 235))
            : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    }

    private void ApplyTheme(string themeMode)
    {
        if (this.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = themeMode switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };

            var effectiveTheme = themeMode switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => rootElement.ActualTheme,
            };
            var isTransitioningToDark = effectiveTheme == ElementTheme.Dark && !_isDarkThemeActive;

            UpdateTitleBarColors(effectiveTheme);
            _isDarkThemeActive = effectiveTheme == ElementTheme.Dark;

            // Theme resource resolution can settle after RequestedTheme changes,
            // so enqueue one more pass to keep the system title bar in sync.
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshTitleBarVisualState();
                ForceNonClientFrameRefresh();
                if (isTransitioningToDark)
                {
                    ForceDarkModeWindowRefresh();
                }
                DispatcherQueue.TryEnqueue(() =>
                {
                    RefreshTitleBarVisualState();
                });
            });
        }
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

    private void ForceNonClientFrameRefresh()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SetWindowPosNoMove | SetWindowPosNoSize | SetWindowPosNoZOrder | SetWindowPosNoActivate | SetWindowPosFrameChanged);
    }

    private void ForceDarkModeWindowRefresh()
    {
        var currentSize = AppWindow.Size;
        if (currentSize.Width <= 0 || currentSize.Height <= 0)
        {
            return;
        }

        AppWindow.Resize(new SizeInt32(currentSize.Width, currentSize.Height + 1));
        DispatcherQueue.TryEnqueue(() =>
        {
            AppWindow.Resize(currentSize);
            RefreshTitleBarVisualState();
        });
    }

    private void UpdateTitleBarInsets()
    {
        var titleBar = AppWindow.TitleBar;
        LeftInsetColumn.Width = new GridLength(titleBar.LeftInset);
        RightInsetColumn.Width = new GridLength(titleBar.RightInset);
    }

    private void UpdateTitleBarColors(ElementTheme actualTheme)
    {
        var isDark = actualTheme == ElementTheme.Dark;
        var titleBar = AppWindow.TitleBar;
        var titleBarBackground = isDark
            ? Windows.UI.Color.FromArgb(255, 32, 32, 32)
            : Windows.UI.Color.FromArgb(255, 243, 243, 243);
        var inactiveBackground = isDark
            ? Windows.UI.Color.FromArgb(255, 40, 40, 40)
            : Windows.UI.Color.FromArgb(255, 248, 248, 248);
        var activeForeground = isDark ? Colors.White : Colors.Black;
        var inactiveForeground = isDark
            ? Windows.UI.Color.FromArgb(255, 160, 160, 160)
            : Windows.UI.Color.FromArgb(255, 128, 128, 128);
        var currentCaptionColor = _isWindowActive ? titleBarBackground : inactiveBackground;
        var currentForeground = _isWindowActive ? activeForeground : inactiveForeground;

        titleBar.ForegroundColor = activeForeground;
        titleBar.BackgroundColor = Colors.Transparent;
        titleBar.InactiveForegroundColor = inactiveForeground;
        titleBar.InactiveBackgroundColor = Colors.Transparent;

        titleBar.ButtonForegroundColor = currentForeground;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonHoverForegroundColor = activeForeground;
        titleBar.ButtonHoverBackgroundColor = isDark
            ? Windows.UI.Color.FromArgb(96, 255, 255, 255)
            : Windows.UI.Color.FromArgb(20, 0, 0, 0);
        titleBar.ButtonPressedForegroundColor = activeForeground;
        titleBar.ButtonPressedBackgroundColor = isDark
            ? Windows.UI.Color.FromArgb(144, 255, 255, 255)
            : Windows.UI.Color.FromArgb(36, 0, 0, 0);

        UpdateTitleBarContentState(currentForeground, _isWindowActive);
        TopEdgeCover.Background = new SolidColorBrush(currentCaptionColor);
        ApplyNativeWindowTheme(currentCaptionColor, currentForeground, isDark);
    }

    private void UpdateTitleBarContentState(Windows.UI.Color foregroundColor, bool isWindowActive)
    {
        AppTitleText.Foreground = new SolidColorBrush(foregroundColor);
        AppIcon.Opacity = isWindowActive ? 1.0 : 0.72;
    }

    private void ApplyNativeWindowTheme(Windows.UI.Color backgroundColor, Windows.UI.Color textColor, bool isDark)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var useDarkMode = isDark ? 1 : 0;
        var borderColor = DwmColorNone;
        var captionColor = ToColorRef(backgroundColor);
        var nativeTextColor = ToColorRef(textColor);

        DwmSetWindowAttribute(hwnd, DwmWindowAttributeUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
        DwmSetWindowAttribute(hwnd, DwmWindowAttributeBorderColor, ref borderColor, sizeof(uint));
        DwmSetWindowAttribute(hwnd, DwmWindowAttributeCaptionColor, ref captionColor, sizeof(uint));
        DwmSetWindowAttribute(hwnd, DwmWindowAttributeTextColor, ref nativeTextColor, sizeof(uint));
    }

    private static uint ToColorRef(Windows.UI.Color color)
    {
        return (uint)(color.R | (color.G << 8) | (color.B << 16));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref uint pvAttribute,
        int cbAttribute);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);
}
