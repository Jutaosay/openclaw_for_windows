// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Dispatching;
using OpenClaw.ViewModels;
using OpenClaw.Views;

namespace OpenClaw;

public sealed partial class MainWindow
{
    private bool _hasPerformedInitialTitleBarRefresh;
    private bool _isDarkThemeActive;
    private bool _hasInitializedWebViewHost;
    private bool _isRecreatingWebView;
    private bool _isWindowActive = true;
    private string? _pendingWebViewRecreationReason;
    private string? _lastWebViewRecreationReason;
    private DateTimeOffset _lastWebViewRecreationRequestedAt = DateTimeOffset.MinValue;
    private readonly DispatcherQueueTimer _runIndicatorTimer;
    private readonly DispatcherQueueTimer _webViewRecreationTimer;
    private bool _isWindowHidden;
    private SettingsDialog? _settingsWindow;
    private int _webViewRecreationCount;
    private int _webViewRecreationMergedCount;
    private string _lastInstrumentationEvent = string.Empty;

    public MainViewModel ViewModel { get; } = new();
}
