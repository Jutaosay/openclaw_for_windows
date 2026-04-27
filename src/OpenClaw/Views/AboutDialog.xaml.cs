// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml.Controls;
using OpenClaw.Helpers;

namespace OpenClaw.Views;

/// <summary>
/// About dialog displaying application name, version, and links.
/// </summary>
public sealed partial class AboutDialog : ContentDialog
{
    public AboutDialog()
    {
        this.InitializeComponent();
        VersionText.Text = $"Version {AppMetadata.GetDisplayVersion()}";
    }
}
