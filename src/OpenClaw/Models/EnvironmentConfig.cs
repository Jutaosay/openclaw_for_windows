// Copyright (c) OpenClaw. All rights reserved.

using System.Text.Json.Serialization;

namespace OpenClaw.Models;

/// <summary>
/// Represents a remote OpenClaw Gateway environment configuration.
/// </summary>
public class EnvironmentConfig
{
    /// <summary>
    /// Gets or sets the display name of this environment (e.g., "Production", "Test").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the gateway URL (e.g., "https://my-claw.example.com").
    /// </summary>
    public string GatewayUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is the default environment loaded on startup.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Creates a deep copy of this environment configuration.
    /// </summary>
    public EnvironmentConfig Clone() => new()
    {
        Name = Name,
        GatewayUrl = GatewayUrl,
        IsDefault = IsDefault,
    };

    public override string ToString() => Name;
}
