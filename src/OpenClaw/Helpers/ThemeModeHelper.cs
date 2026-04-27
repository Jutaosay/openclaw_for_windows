// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;

namespace OpenClaw.Helpers;

/// <summary>
/// Centralizes theme mode parsing so windows can share the same semantics.
/// </summary>
internal static class ThemeModeHelper
{
    public const string System = "System";
    public const string Light = "Light";
    public const string Dark = "Dark";

    public static ElementTheme ToElementTheme(string? themeMode)
    {
        return themeMode switch
        {
            Light => ElementTheme.Light,
            Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    public static ElementTheme GetEffectiveTheme(FrameworkElement rootElement, string? themeMode)
    {
        return themeMode switch
        {
            Light => ElementTheme.Light,
            Dark => ElementTheme.Dark,
            _ => rootElement.ActualTheme,
        };
    }
}
