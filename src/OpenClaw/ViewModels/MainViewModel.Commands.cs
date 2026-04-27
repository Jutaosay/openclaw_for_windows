// Copyright (c) Lanstack @openclaw. All rights reserved.

using OpenClaw.Helpers;
using OpenClaw.Services;

namespace OpenClaw.ViewModels;

public partial class MainViewModel
{
    private void InitializeCommands()
    {
        OpenSettingsCommand = new SimpleCommand(() => OpenSettingsRequested?.Invoke());
        ReloadCommand = new SimpleCommand(OnReload);
        StopCommand = new SimpleCommand(OnStop);
        RetryCommand = new SimpleCommand(OnRetry);
        DevToolsCommand = new SimpleCommand(OnDevTools);
        RunDiagnosticsCommand = new AsyncCommand(OnRunDiagnosticsAsync, OnAsyncCommandFailed);
        ViewLogsCommand = new SimpleCommand(() => ViewLogsRequested?.Invoke());
    }

    private void OnAsyncCommandFailed(Exception ex)
    {
        App.Logger.Error($"Async command failed: {ex}");
    }

    private void OnRetry()
    {
        IsErrorVisible = false;
        _webViewService.RetryNavigation();
    }

    private void OnReload()
    {
        _webViewService.Reload();
    }

    private void OnStop()
    {
        _webViewService.Stop();
    }

    private void OnDevTools()
    {
        _webViewService.OpenDevTools();
    }

    /// <summary>
    /// Dismisses the error InfoBar.
    /// </summary>
    public void DismissError()
    {
        IsErrorVisible = false;
    }

    public void DismissDiagnostics()
    {
        IsDiagnosticVisible = false;
    }

    private async Task OnRunDiagnosticsAsync()
    {
        App.Logger.Info("Running diagnostics...");

        var gatewayUrl = _selectedEnvironment?.GatewayUrl;
        var report = await DiagnosticService.RunAllAsync(gatewayUrl, _webViewService);
        _coordinator?.UpdateInstrumentation(
            totalControlUiInspectionRequests: _webViewService.TotalControlUiInspectionRequests,
            cachedControlUiInspectionRequests: _webViewService.CachedControlUiInspectionRequests,
            coalescedControlUiInspectionRequests: _webViewService.CoalescedControlUiInspectionRequests,
            deferredSaveRequests: App.Configuration.DeferredSaveRequests,
            deferredSaveCoalescedRequests: App.Configuration.DeferredSaveCoalescedRequests,
            heartbeatRecoveryRequests: _webViewService.HeartbeatRecoveryRequests,
            lastInstrumentationEvent: "diagnostics.run");

        DiagnosticSummary = report.ToSummary();
        IsDiagnosticVisible = true;

        App.Logger.Info($"Diagnostics complete. Failures: {report.HasFailures}");
    }
}
