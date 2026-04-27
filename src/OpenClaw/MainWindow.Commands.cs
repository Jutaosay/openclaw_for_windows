// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClaw.Views;

namespace OpenClaw;

public sealed partial class MainWindow
{
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
            ActivateSettingsWindow();
            return;
        }

        _settingsWindow = CreateSettingsWindow();
        ActivateSettingsWindow();
    }

    private void OnSettingsSaved(SettingsSaveResult saveResult)
    {
        HandleSettingsSaved(saveResult);
    }

    private SettingsDialog CreateSettingsWindow()
    {
        var settingsWindow = new SettingsDialog
        {
            MainViewModel = this.ViewModel,
        };

        settingsWindow.SettingsSaved += OnSettingsSaved;
        settingsWindow.Closed += OnSettingsWindowClosed;
        return settingsWindow;
    }

    private void ActivateSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            return;
        }

        _settingsWindow.SyncWithCurrentSettings();
        _settingsWindow.Activate();
    }

    private void OnSettingsWindowClosed(object sender, WindowEventArgs args)
    {
        if (_settingsWindow is null)
        {
            return;
        }

        _settingsWindow.SettingsSaved -= OnSettingsSaved;
        _settingsWindow.Closed -= OnSettingsWindowClosed;
        _settingsWindow = null;
    }

    private void HandleSettingsSaved(SettingsSaveResult saveResult)
    {
        if (saveResult.DidChangeEnvironmentState)
        {
            ViewModel.RefreshEnvironments();
        }

        if (saveResult.DidChangeSessionTopology)
        {
            ScheduleWebViewRecreation("settings_saved_topology_changed");
        }
    }

    private void OnError(string message)
    {
        App.Logger.Error($"UI error displayed: {message}");
    }

    private void OnWebViewRecreationRequested(string reason)
    {
        ScheduleWebViewRecreation(reason);
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

    private async void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog
        {
            XamlRoot = this.Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
