// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace OpenClaw.Models;

/// <summary>
/// Represents a remote OpenClaw Gateway environment configuration.
/// </summary>
public class EnvironmentConfig : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _gatewayUrl = string.Empty;
    private bool _isDefault;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets the display name of this environment (e.g., "Production", "Test").
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Gets or sets the gateway URL (e.g., "https://my-claw.example.com").
    /// </summary>
    public string GatewayUrl
    {
        get => _gatewayUrl;
        set => SetProperty(ref _gatewayUrl, value);
    }

    /// <summary>
    /// Gets or sets whether this is the default environment loaded on startup.
    /// </summary>
    public bool IsDefault
    {
        get => _isDefault;
        set => SetProperty(ref _isDefault, value);
    }

    /// <summary>
    /// Creates a deep copy of this environment configuration.
    /// </summary>
    public EnvironmentConfig Clone() => new()
    {
        Name = Name,
        GatewayUrl = GatewayUrl,
        IsDefault = IsDefault,
    };

    /// <summary>
    /// Updates this instance with values from another environment configuration.
    /// </summary>
    public void ApplyFrom(EnvironmentConfig other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Name = other.Name;
        GatewayUrl = other.GatewayUrl;
        IsDefault = other.IsDefault;
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => Name;
}
