// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;
using OpenClaw.Services;

namespace OpenClaw;

/// <summary>
/// The application entry point. Initializes services and creates the main window.
/// </summary>
public partial class App : Application
{
    private Window? _mainWindow;
    private SingleInstanceCoordinator? _singleInstanceCoordinator;

    static App()
    {
        OpenClaw.Services.AppTelemetry.DeferredSaveRequestsProvider = () => Configuration.DeferredSaveRequests;
        OpenClaw.Services.AppTelemetry.DeferredSaveCoalescedRequestsProvider = () => Configuration.DeferredSaveCoalescedRequests;
    }

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Gets the singleton <see cref="LoggingService"/> instance.
    /// </summary>
    public static LoggingService Logger { get; } = new();

    /// <summary>
    /// Gets the singleton <see cref="ConfigurationService"/> instance.
    /// </summary>
    public static ConfigurationService Configuration { get; } = new(Logger);

    /// <summary>
    /// Gets the main application window.
    /// </summary>
    public static Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Logger.Info("Application launching.");
        Configuration.Load();
        Logger.Info("Configuration loaded.");

        if (!Configuration.Settings.AllowMultipleInstances && RedirectSecondaryLaunchToPrimaryInstance())
        {
            Exit();
            return;
        }

        // Apply saved language preference
        ApplyLanguage(Configuration.Settings.AppLanguage);
        Logger.Info("Language applied.");

        Logger.Info("Creating main window.");
        _mainWindow = new MainWindow();
        _mainWindow.Closed += OnMainWindowClosed;
        Logger.Info("Main window created.");
        MainWindow = _mainWindow;
        _mainWindow.Activate();

        Logger.Info("Main window activated.");
    }

    private bool RedirectSecondaryLaunchToPrimaryInstance()
    {
        _singleInstanceCoordinator = SingleInstanceCoordinator.CreatePrimaryOrSecondary(Logger);
        if (_singleInstanceCoordinator.IsPrimary)
        {
            _singleInstanceCoordinator.ActivationRequested += OnPrimaryInstanceActivationRequested;
            _singleInstanceCoordinator.StartListening();
            return false;
        }

        Logger.Info("Secondary launch detected; requesting primary instance activation.");
        SingleInstanceCoordinator.RequestActivationOfPrimaryInstance(Logger);
        _singleInstanceCoordinator.Dispose();
        _singleInstanceCoordinator = null;
        return true;
    }

    private void OnPrimaryInstanceActivationRequested()
    {
        Logger.Info("Primary instance activation requested.");
        if (_mainWindow is not MainWindow mainWindow)
        {
            return;
        }

        mainWindow.DispatcherQueue.TryEnqueue(mainWindow.ActivateFromExternalLaunch);
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        if (_mainWindow is not null)
        {
            _mainWindow.Closed -= OnMainWindowClosed;
        }

        _singleInstanceCoordinator?.Dispose();
        _singleInstanceCoordinator = null;
    }

    /// <summary>
    /// Applies the language override. "System" skips override to follow OS language.
    /// </summary>
    public static void ApplyLanguage(string language)
    {
        try
        {
            if (string.IsNullOrEmpty(language) || language == "System")
            {
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = string.Empty;
            }
            else
            {
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = language;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Language override failed: {ex.Message}");
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Logger.Error($"Unhandled exception: {e.Exception}");
    }
}
