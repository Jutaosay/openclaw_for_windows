// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenClaw;

public sealed partial class MainWindow
{
    private void ConfigureWindowChrome()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);
        UpdateTitleBarInsets();

        Title = "OpenClaw";
        AppWindow.SetIcon("Assets\\WindowIcon.ico");
        SystemBackdrop = new MicaBackdrop
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt,
        };
    }

    private void SubscribeToViewModelEvents()
    {
        ViewModel.OpenSettingsRequested += OnOpenSettingsRequested;
        ViewModel.WebViewRecreationRequested += OnWebViewRecreationRequested;
        ViewModel.ViewLogsRequested += OnViewLogsRequested;
        ViewModel.ErrorOccurred += OnError;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private DispatcherQueueTimer CreateRunIndicatorTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(430);
        timer.Tick += OnRunIndicatorTick;
        return timer;
    }

    private DispatcherQueueTimer CreateWebViewRecreationTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(150);
        timer.Tick += OnWebViewRecreationTimerTick;
        return timer;
    }

    private void AttachWindowEventHandlers()
    {
        this.Closed += OnWindowClosed;
        this.Activated += OnWindowActivated;
    }

    private void AttachRootEventHandlers()
    {
        if (this.Content is not FrameworkElement rootElement)
        {
            return;
        }

        rootElement.Loaded += OnRootLoaded;
        rootElement.ActualThemeChanged += OnRootActualThemeChanged;
    }
}
