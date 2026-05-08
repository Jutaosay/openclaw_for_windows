// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace OpenClaw.Views;

public sealed partial class SettingsDialog
{
    private void ConfigureWindowChrome()
    {
        Title = Helpers.StringResources.SettingsTitle;
        AppWindow.SetIcon("Assets\\WindowIcon.ico");
        AppWindow.Resize(new SizeInt32(720, 520));
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
        ApplyTheme(App.Configuration.Settings.AppTheme);
    }

    private void InitializeEnvironmentBindings()
    {
        EnvironmentList.ItemsSource = ViewModel.Environments;
        SelectFirstEnvironment();
    }

    private void InitializeNavigationState()
    {
        PopulateLanguageOptions();
        NavList.SelectedIndex = 0;
    }
}
