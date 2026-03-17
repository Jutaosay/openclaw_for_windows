// Copyright (c) OpenClaw. All rights reserved.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Services;
using OpenClaw.ViewModels;
using OpenClaw.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;

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

        // Phase 3: Wire up drag-drop on the WebView2 area
        MainWebView.AllowDrop = true;
        MainWebView.DragOver += OnDragOver;
        MainWebView.Drop += OnFileDrop;

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

    private async void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        await ShowSettingsDialogAsync();
    }

    private async void OnOpenSettingsRequested()
    {
        await ShowSettingsDialogAsync();
    }

    private async Task ShowSettingsDialogAsync()
    {
        var dialog = new SettingsDialog
        {
            XamlRoot = this.Content.XamlRoot,
            MainViewModel = this.ViewModel,
        };

        var result = await dialog.ShowAsync();

        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            // Settings were saved, refresh environments
            ViewModel.RefreshEnvironments();
        }
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

    // --- Phase 3: Drag and Drop ---

    private void OnDragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Drop to upload";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsGlyphVisible = true;
    }

    private async void OnFileDrop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = items
                .OfType<StorageFile>()
                .Select(f => f.Path)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (paths.Length > 0)
            {
                await ViewModel.HandleFileDropAsync(paths);
            }
        }
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

    private void OnThemeSelected(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string themeStr)
        {
            ApplyTheme(themeStr);
            
            // Save preference
            App.Configuration.Settings.AppTheme = themeStr;
            App.Configuration.Save();
        }
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
            switch (themeMode)
            {
                case "Light":
                    rootElement.RequestedTheme = ElementTheme.Light;
                    ThemeIcon.Glyph = "\uE706"; // Light icon
                    break;
                case "Dark":
                    rootElement.RequestedTheme = ElementTheme.Dark;
                    ThemeIcon.Glyph = "\uE708"; // Dark icon
                    break;
                case "System":
                default:
                    rootElement.RequestedTheme = ElementTheme.Default;
                    ThemeIcon.Glyph = "\uE7F4"; // System icon
                    break;
            }
        }
    }
}
