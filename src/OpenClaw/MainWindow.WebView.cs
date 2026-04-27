// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OpenClaw;

public sealed partial class MainWindow
{
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.IsRunIndicatorsAnimating))
        {
            UpdateRunIndicatorAnimationState();
        }
    }

    private void UpdateRunIndicatorAnimationState()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ViewModel.IsRunIndicatorsAnimating && !_isWindowHidden)
            {
                if (!_runIndicatorTimer.IsRunning)
                {
                    _runIndicatorTimer.Start();
                }
            }
            else
            {
                _runIndicatorTimer.Stop();
            }
        });
    }

    private void OnRunIndicatorTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        ViewModel.AdvanceRunIndicators();
    }

    private void OnWebViewRecreationTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        _webViewRecreationTimer.Stop();
        _ = RecreateWebViewAsync();
    }

    private void RecordInstrumentationEvent(string eventName, object? context = null)
    {
        _lastInstrumentationEvent = eventName;
        App.Logger.Info(eventName, context);

        if (ViewModel.Coordinator is not null)
        {
            ViewModel.Coordinator.UpdateInstrumentation(
                totalWebViewRecreations: _webViewRecreationCount,
                mergedWebViewRecreationRequests: _webViewRecreationMergedCount,
                lastInstrumentationEvent: _lastInstrumentationEvent);
        }
    }

    private void ScheduleWebViewRecreation(string reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason;
        if (_pendingWebViewRecreationReason is not null)
        {
            _webViewRecreationMergedCount++;
        }

        _pendingWebViewRecreationReason = normalizedReason;
        _lastWebViewRecreationRequestedAt = DateTimeOffset.UtcNow;

        RecordInstrumentationEvent("webview.recreation.queued", new
        {
            reason = normalizedReason,
            isRecreating = _isRecreatingWebView,
            merged = _webViewRecreationMergedCount
        });

        if (_isRecreatingWebView)
        {
            return;
        }

        if (_webViewRecreationTimer.IsRunning)
        {
            _webViewRecreationTimer.Stop();
        }

        _webViewRecreationTimer.Start();
    }

    private async Task RecreateWebViewAsync()
    {
        if (_isRecreatingWebView)
        {
            return;
        }

        var pendingReason = _pendingWebViewRecreationReason;
        _pendingWebViewRecreationReason = null;
        if (pendingReason is null)
        {
            if (WebViewHost.Children.Count == 0)
            {
                pendingReason = "implicit_initial_load";
                RecordInstrumentationEvent("webview.recreation.recovered_missing_reason", new
                {
                    reason = pendingReason,
                    lastReason = _lastWebViewRecreationReason
                });
            }
            else
            {
                return;
            }
        }

        _isRecreatingWebView = true;
        _lastWebViewRecreationReason = pendingReason;
        RecordInstrumentationEvent("webview.recreation.started", new { reason = pendingReason });

        try
        {
            do
            {
                var nextWebView = new WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };

                WebViewHost.Children.Clear();
                WebViewHost.Children.Add(nextWebView);

                _webViewRecreationCount++;
                RecordInstrumentationEvent("webview.recreation.initializing", new
                {
                    reason = _lastWebViewRecreationReason,
                    total = _webViewRecreationCount
                });
                await ViewModel.InitializeWebViewAsync(nextWebView);
            }
            while (TryConsumeQueuedWebViewRecreation());
        }
        catch (Exception ex)
        {
            App.Logger.Error($"Failed to recreate WebView2 host: {ex.Message}");
        }
        finally
        {
            _isRecreatingWebView = false;

            if (_pendingWebViewRecreationReason is not null)
            {
                ScheduleWebViewRecreation(_pendingWebViewRecreationReason);
            }

            RecordInstrumentationEvent("webview.recreation.finished", new
            {
                lastReason = _lastWebViewRecreationReason,
                pendingReason = _pendingWebViewRecreationReason,
                total = _webViewRecreationCount,
                merged = _webViewRecreationMergedCount
            });
        }
    }

    private bool TryConsumeQueuedWebViewRecreation()
    {
        if (_pendingWebViewRecreationReason is null)
        {
            return false;
        }

        _lastWebViewRecreationReason = _pendingWebViewRecreationReason;
        _pendingWebViewRecreationReason = null;
        return true;
    }
}
