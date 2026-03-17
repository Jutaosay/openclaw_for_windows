// Copyright (c) OpenClaw. All rights reserved.

using Microsoft.Windows.ApplicationModel.Resources;

namespace OpenClaw.Helpers;

/// <summary>
/// Provides typed access to .resw string resources.
/// Centralizes all user-facing strings for future i18n support.
/// </summary>
public static class StringResources
{
    private static readonly ResourceLoader _loader = new();

    /// <summary>
    /// Gets a string resource by key.
    /// </summary>
    public static string Get(string key)
    {
        try
        {
            return _loader.GetString(key);
        }
        catch
        {
            return key; // Fallback to key name
        }
    }

    // --- Top Bar ---
    public static string Reload => Get("Reload");
    public static string Stop => Get("Stop");
    public static string OpenInBrowser => Get("OpenInBrowser");
    public static string Settings => Get("Settings");
    public static string ClearSession => Get("ClearSession");

    // --- Status Bar ---
    public static string StatusConnected => Get("StatusConnected");
    public static string StatusLoading => Get("StatusLoading");
    public static string StatusReconnecting => Get("StatusReconnecting");
    public static string StatusAuthFailed => Get("StatusAuthFailed");
    public static string StatusError => Get("StatusError");
    public static string StatusOffline => Get("StatusOffline");

    // --- Settings Dialog ---
    public static string SettingsTitle => Get("SettingsTitle");
    public static string EnvironmentName => Get("EnvironmentName");
    public static string GatewayUrl => Get("GatewayUrl");
    public static string SetAsDefault => Get("SetAsDefault");
    public static string AddEnvironment => Get("AddEnvironment");
    public static string RemoveEnvironment => Get("RemoveEnvironment");
    public static string Save => Get("Save");
    public static string Cancel => Get("Cancel");
    public static string NoEnvironments => Get("NoEnvironments");
}
