// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Reflection;

namespace OpenClaw.Helpers;

/// <summary>
/// Provides centralized application metadata for UI and docs-facing surfaces.
/// </summary>
internal static class AppMetadata
{
    public static string GetDisplayVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is not null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";
    }
}
