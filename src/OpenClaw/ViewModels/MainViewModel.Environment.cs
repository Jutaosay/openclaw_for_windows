// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Models;

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    private void LoadEnvironments()
    {
        Environments.Clear();

        foreach (var environment in App.Configuration.Settings.Environments)
        {
            Environments.Add(environment);
        }

        UpdateEnvironmentSelection(App.Configuration.GetSelectedEnvironment(), persistSelection: false);
    }

    private void UpdateEnvironmentSelection(EnvironmentConfig? environment, bool persistSelection)
    {
        _selectedEnvironment = environment;
        OnPropertyChanged(nameof(SelectedEnvironment));
        OnPropertyChanged(nameof(CurrentUrl));
        OnPropertyChanged(nameof(SelectedEnvironmentName));

        if (environment is null)
        {
            RefreshResourceScheduling();
            ResetTelemetry();
            return;
        }

        ResetTelemetry();
        _coordinator?.Reset();
        _coordinator?.SetEnvironment(environment.Name, environment.GatewayUrl);
        UpdateStatusPresentation();
        RefreshResourceScheduling();

        if (persistSelection)
        {
            App.Configuration.Settings.SelectedEnvironmentName = environment.Name;
            App.Configuration.SaveDeferred();
        }

        if (!_webViewService.IsInitialized)
        {
            return;
        }

        if (_webViewService.IsUsingEnvironmentProfile(environment.Name))
        {
            _webViewService.Navigate(environment.GatewayUrl);
        }
        else
        {
            WebViewRecreationRequested?.Invoke("environment_profile_changed");
        }
    }

    public async Task ClearSessionForEnvironmentAsync(string environmentName)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            return;
        }

        await _webViewService.ClearEnvironmentSessionAsync(environmentName);

        if (string.Equals(_selectedEnvironment?.Name, environmentName, StringComparison.Ordinal))
        {
            DismissError();
            DismissDiagnostics();
            WebViewRecreationRequested?.Invoke("active_session_cleared");
        }
    }
}
