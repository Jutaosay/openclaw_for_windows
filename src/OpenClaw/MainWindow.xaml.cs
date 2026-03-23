// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Services;
using OpenClaw.ViewModels;
using OpenClaw.Views;
using Windows.Graphics;

namespace OpenClaw;

/// <summary>
/// The main application window. Hosts the WebView2 control, top bar, and status bar.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        this.InitializeComponent();

        // Set title and system backdrop
        Title = "OpenClaw";
        AppWindow.SetIcon("Assets\\WindowIcon.ico");
        SystemBackdrop = new MicaBackdrop();

        // Restore window size/position
        RestoreWindowBounds();

        // Wire up events
        ViewModel.OpenSettingsRequested += OnOpenSettingsRequested;
        ViewModel.ViewLogsRequested += OnViewLogsRequested;
        ViewModel.ErrorOccurred += OnError;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Initialize WebView2 when the window is loaded
        MainWebView.Loaded += async (s, e) =>
        {
            await ViewModel.InitializeWebViewAsync(MainWebView);
        };

        // Save window bounds on close
        this.Closed += OnWindowClosed;

        // Apply saved theme after content is loaded
        if (this.Content is FrameworkElement rootElement)
        {
            rootElement.Loaded += (s, e) =>
            {
                ApplyTheme(App.Configuration.Settings.AppTheme);
            };
        }

        // Set initial status dot color
        UpdateStatusDotColor(ConnectionState.Offline);
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
        SaveWindowBounds();
        App.Logger.Info("Application closing.");
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

        _settingsWindow.SettingsSaved += () =>
        {
            ViewModel.RefreshEnvironments();
            ApplyTheme(App.Configuration.Settings.AppTheme);
        };

        _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        _settingsWindow.Activate();
    }

    private void OnError(string message)
    {
        // Error is now displayed via the InfoBar binding
        App.Logger.Error($"UI error displayed: {message}");
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
        }
    }

    private void UpdateStatusDotColor(ConnectionState state)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var brushKey = state switch
            {
                ConnectionState.Connected => "StatusConnectedBrush",
                ConnectionState.Loading or ConnectionState.Reconnecting => "StatusReconnectingBrush",
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

            // Update title bar colors to match theme
            var isDark = rootElement.ActualTheme == ElementTheme.Dark;
            var titleBar = AppWindow.TitleBar;

            titleBar.ForegroundColor = isDark ? Colors.White : Colors.Black;
            titleBar.BackgroundColor = isDark
                ? Windows.UI.Color.FromArgb(255, 32, 32, 32)
                : Windows.UI.Color.FromArgb(255, 243, 243, 243);
            titleBar.InactiveForegroundColor = isDark
                ? Windows.UI.Color.FromArgb(255, 160, 160, 160)
                : Windows.UI.Color.FromArgb(255, 128, 128, 128);
            titleBar.InactiveBackgroundColor = titleBar.BackgroundColor;

            titleBar.ButtonForegroundColor = titleBar.ForegroundColor;
            titleBar.ButtonBackgroundColor = titleBar.BackgroundColor;
            titleBar.ButtonInactiveForegroundColor = titleBar.InactiveForegroundColor;
            titleBar.ButtonInactiveBackgroundColor = titleBar.InactiveBackgroundColor;
            titleBar.ButtonHoverForegroundColor = isDark ? Colors.White : Colors.Black;
            titleBar.ButtonHoverBackgroundColor = isDark
                ? Windows.UI.Color.FromArgb(255, 51, 51, 51)
                : Windows.UI.Color.FromArgb(255, 229, 229, 229);
        }
    }
}
