// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Helpers;
using OpenClaw.ViewModels;

namespace OpenClaw.Views;

public readonly record struct SettingsSaveResult(
    bool DidChangeEnvironmentState,
    bool DidChangeSessionTopology,
    bool DidChangeLanguage);

public sealed partial class SettingsDialog
{
    private const string LanguagePanelTag = "Language";
    private const string EnvironmentsPanelTag = "Environments";
    private const string SessionsPanelTag = "Sessions";
    private const string DevToolsPanelTag = "DevTools";
    private bool _hasPerformedInitialTitleBarRefresh;
    private bool _isDarkThemeActive;

    public SettingsViewModel ViewModel { get; } = new();

    /// <summary>
    /// Gets the main view model for developer tools commands.
    /// </summary>
    public MainViewModel? MainViewModel { get; set; }

    /// <summary>
    /// Raised when settings are saved, so MainWindow can refresh.
    /// </summary>
    public event Action<SettingsSaveResult>? SettingsSaved;

    private static string ValidationErrorTitle => StringResources.SettingsValidationError;
}
