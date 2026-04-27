// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenClaw;

/// <summary>
/// The main application window. Hosts the WebView2 control, top bar, and status bar.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        ConfigureWindowChrome();
        RestoreWindowBounds();
        SubscribeToViewModelEvents();
        _runIndicatorTimer = CreateRunIndicatorTimer();
        _webViewRecreationTimer = CreateWebViewRecreationTimer();
        AttachWindowEventHandlers();
        AttachRootEventHandlers();
        UpdateThemeSelector(App.Configuration.Settings.AppTheme);
    }
}
