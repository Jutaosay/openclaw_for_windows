// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Reflection;
using Microsoft.UI.Xaml.Controls;

namespace OpenClaw.Views;

/// <summary>
/// About dialog displaying application name, version, and links.
/// </summary>
public sealed partial class AboutDialog : ContentDialog
{
    public AboutDialog()
    {
        this.InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is not null
            ? $"Version {version.Major}.{version.Minor}.{version.Build}"
            : "Version 1.0.5";
    }
}
