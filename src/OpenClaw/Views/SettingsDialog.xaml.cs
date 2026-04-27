// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace OpenClaw.Views;

/// <summary>
/// Settings window with Windows Settings-style sidebar navigation.
/// Resizable, Mica-backed, independent window.
/// </summary>
public sealed partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        this.InitializeComponent();
        ConfigureWindowChrome();
        AttachRootEventHandlers();
        InitializeEnvironmentBindings();
        InitializeNavigationState();
    }
}
