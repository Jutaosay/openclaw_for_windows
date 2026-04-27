// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClaw.Helpers;

namespace OpenClaw.Views;

public sealed partial class SettingsDialog
{
    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (!TryApplyEdit())
        {
            return;
        }

        ValidationInfoBar.IsOpen = false;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TrySaveSettings(out var saveResult))
        {
            return;
        }

        if (saveResult.DidChangeLanguage)
        {
            App.ApplyLanguage(ViewModel.SelectedLanguage);
        }

        SettingsSaved?.Invoke(saveResult);
        this.Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void OnRunDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        MainViewModel?.RunDiagnosticsCommand.Execute(null);
        this.Close();
    }

    private void OnViewLogsClick(object sender, RoutedEventArgs e)
    {
        MainViewModel?.ViewLogsCommand.Execute(null);
    }

    private void OnDevToolsClick(object sender, RoutedEventArgs e)
    {
        MainViewModel?.DevToolsCommand.Execute(null);
        this.Close();
    }

    private async void OnClearEnvironmentSessionClick(object sender, RoutedEventArgs e)
    {
        if (MainViewModel is null ||
            sender is not Button button ||
            button.Tag is not string environmentName ||
            string.IsNullOrWhiteSpace(environmentName))
        {
            ShowSessionMessage(InfoBarSeverity.Warning, StringResources.SettingsSessionResetSelectEnvironment);
            return;
        }

        await MainViewModel.ClearSessionForEnvironmentAsync(environmentName);
        ShowSessionMessage(
            InfoBarSeverity.Informational,
            string.Format(StringResources.SettingsSessionResetCompleted, environmentName));
    }

    private void ShowEnvironmentMessage(string title, InfoBarSeverity severity, string? message = null)
    {
        ValidationInfoBar.Title = title;
        ValidationInfoBar.Severity = severity;
        ValidationInfoBar.Message = message ?? ViewModel.ValidationMessage;
        ValidationInfoBar.IsOpen = true;
    }

    private void ShowSessionMessage(InfoBarSeverity severity, string message)
    {
        SessionInfoBar.Title = StringResources.SettingsSessionReset;
        SessionInfoBar.Severity = severity;
        SessionInfoBar.Message = message;
        SessionInfoBar.IsOpen = true;
    }

    private bool TryApplyEdit()
    {
        if (ViewModel.TryApplyEdit())
        {
            return true;
        }

        ShowEnvironmentMessage(ValidationErrorTitle, InfoBarSeverity.Error);
        return false;
    }

    private bool TrySaveSettings(out SettingsSaveResult result)
    {
        result = default;
        if (ViewModel.IsEditing && !TryApplyEdit())
        {
            return false;
        }

        if (ViewModel.SaveAll(out result))
        {
            return true;
        }

        ShowEnvironmentMessage(ValidationErrorTitle, InfoBarSeverity.Error);
        return false;
    }
}
