// Copyright (c) OpenClaw. All rights reserved.

using Microsoft.UI.Xaml;
using OpenClaw.Services;

namespace OpenClaw;

/// <summary>
/// The application entry point. Initializes services and creates the main window.
/// </summary>
public partial class App : Application
{
    private Window? _mainWindow;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Gets the singleton <see cref="ConfigurationService"/> instance.
    /// </summary>
    public static ConfigurationService Configuration { get; } = new();

    /// <summary>
    /// Gets the singleton <see cref="LoggingService"/> instance.
    /// </summary>
    public static LoggingService Logger { get; } = new();

    /// <summary>
    /// Gets the main application window.
    /// </summary>
    public static Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Logger.Info("Application launching.");
        Configuration.Load();
        Logger.Info("Configuration loaded.");

        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        _mainWindow.Activate();

        Logger.Info("Main window activated.");
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Logger.Error($"Unhandled exception: {e.Exception}");
        e.Handled = true;
    }
}
