// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Reflection;

namespace OpenClaw.Helpers;

/// <summary>
/// Provides centralized application metadata for UI and docs-facing surfaces.
/// </summary>
internal static class AppMetadata
{
    public const string CurrentVersion = "3.0.3";

    public static string GetDisplayVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is not null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : CurrentVersion;
    }
}
