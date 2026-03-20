// Copyright (c) OpenClaw. All rights reserved.
// Phase 5: Upstream-compatible abstraction interfaces.
// These isolate the generic shell/hosting concepts from OpenClaw-specific logic,
// enabling potential reuse in OpenClaw Control or other WebView2-based shells.

namespace OpenClaw.Abstractions;

/// <summary>
/// Abstraction for a remote environment that the shell connects to.
/// Generic enough to be reused by any WebView2-hosting management shell.
/// </summary>
public interface IRemoteEnvironment
{
    string Name { get; }
    string Url { get; }
    bool IsDefault { get; }
}

/// <summary>
/// Abstraction for WebView2 hosting and lifecycle management.
/// Separates the generic browser-hosting concerns from app-specific logic.
/// </summary>
public interface IWebViewHost
{
    bool IsInitialized { get; }
    string? CurrentUrl { get; }

    void Navigate(string url);
    void Reload();
    void StopNavigation();
    void OpenDevTools();

    Task ClearBrowsingDataAsync();
    bool RetryNavigation();

    event Action<HostConnectionState>? ConnectionStateChanged;
    event Action<string>? NavigationErrorOccurred;
}

/// <summary>
/// Generic connection state, decoupled from the OpenClaw-specific enum.
/// </summary>
public enum HostConnectionState
{
    Offline,
    Loading,
    Connected,
    Reconnecting,
    AuthFailed,
    Error,
}

/// <summary>
/// Abstraction for command injection into the hosted remote UI.
/// This separates the "how to send commands" from "what commands to send".
/// </summary>
public interface ICommandInjector
{
    Task<bool> InjectCommandAsync(string command);
    Task<bool> InjectStopCommandAsync();
}

/// <summary>
/// Abstraction for startup diagnostics.
/// </summary>
public interface IDiagnosticRunner
{
    Task<string> RunDiagnosticsAsync(string? targetUrl);
}

/// <summary>
/// Abstraction for configuration persistence.
/// </summary>
public interface IConfigurationStore<T> where T : class, new()
{
    T Settings { get; }
    void Load();
    void Save();
}
