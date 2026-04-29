# OpenClaw Manager

Lightweight Windows-native OpenClaw remote management shell built with WinUI 3 and WebView2.

OpenClaw Manager is a thin desktop shell for the hosted OpenClaw Control UI. It is designed for remote Gateway deployments running on a VPS and exposed through Cloudflare Tunnel, reverse proxy, or another public HTTPS origin.

---

## Overview

This project keeps the existing OpenClaw web experience, but wraps it in a native WinUI 3 window with:

- environment switching
- per-environment WebView2 session isolation
- connection recovery and heartbeat monitoring
- diagnostics and structured logs
- native theme and window integration

It is best suited for users who:

- run OpenClaw Gateway remotely
- access it through Cloudflare Tunnel or a reverse proxy
- want a lightweight Windows-native client instead of keeping a browser tab open

### This project is

- A WinUI 3 + WebView2 remote management shell
- A Windows-native entry point for hosted OpenClaw Control UI sessions
- A thin client that enhances the existing web UI with native UX

### This project is not

- A local Gateway or node host
- A full native rewrite of the OpenClaw frontend
- An offline-capable standalone application

---

## Tech Stack

| Component | Version |
|---|---|
| .NET | 10.0 |
| Windows App SDK | 1.8.x |
| WebView2 | Bundled via WinAppSDK |
| MVVM | CommunityToolkit.Mvvm 8.x |
| UI Language | English + Simplified Chinese |
| Packaging | Unpackaged, self-contained |

---

## Project Structure

```text
Claw_winui3/
|-- NuGet.config
|-- OpenClaw.sln
|-- DEVELOPMENT_NOTES.md
|-- README.md
`-- src/OpenClaw/
    |-- OpenClaw.csproj
    |-- Package.appxmanifest
    |-- app.manifest
    |-- App.xaml
    |-- App.xaml.cs
    |-- MainWindow.xaml
    |-- MainWindow.xaml.cs
    |-- Assets/
    |-- Abstractions/
    |-- Helpers/
    |-- Models/
    |-- Services/
    |-- Strings/
    |-- ViewModels/
    `-- Views/
```

### Key folders

- `Services/`: configuration, logging, diagnostics, WebView2 lifecycle, recovery helpers
- `ViewModels/`: shell state, commands, settings editing
- `Views/`: settings, about, and log viewer dialogs
- `Strings/`: localized UI resources

---

## Runtime Requirements

The compiled application requires:

| Dependency | Download |
|---|---|
| .NET 10 Desktop Runtime | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| WebView2 Runtime | [Download](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) |

Windows 11 usually already includes WebView2 Runtime. Windows 10 users may need to install it manually.

---

## Development Prerequisites

- Windows 10 1809+ or Windows 11
- Visual Studio 2026
- .NET Desktop Development workload
- Windows App SDK C# templates
- .NET 10 SDK

### Dependency Restore Notes

- The solution uses SDK-style projects with `PackageReference`.
- Repository-local `packages/` folders are not required and can be deleted safely.
- A solution-level `[Directory.Build.props](/C:/Users/Zen/Repo/Codings/Claw_winui3/Directory.Build.props)` enables `RestorePackagesConfig=true` so full Visual Studio / MSBuild builds still auto-restore if a future `packages.config` project is added.
- The expected workflow after clearing local caches is simply:

```powershell
dotnet restore OpenClaw.sln
dotnet build OpenClaw.sln
```

### Development Notes

See [DEVELOPMENT_NOTES.md](DEVELOPMENT_NOTES.md) for lessons learned from native window chrome, theme synchronization, and other maintenance-sensitive areas.

---

## Getting Started

### Visual Studio

1. Open [OpenClaw.sln](/C:/Users/Zen/Repo/Codings/Claw_winui3/OpenClaw.sln) in Visual Studio 2026.
2. Set solution platform to `x64`.
3. Press `F5` to run.

### CLI

```powershell
dotnet restore src\OpenClaw\OpenClaw.csproj
dotnet build src\OpenClaw\OpenClaw.csproj -r win-x64
```

### First Launch

1. The app starts with a placeholder environment.
2. Open Settings from the top bar.
3. Add your public OpenClaw Control UI URL, for example `https://your-gateway.example.com`.
4. Save settings and the embedded WebView2 shell will load the remote UI.

### Cloudflare Tunnel / VPS Notes

If your OpenClaw Gateway runs on a VPS behind Cloudflare Tunnel:

- use the public HTTPS Control UI URL in OpenClaw Manager
- do not use the raw Gateway WebSocket URL
- make sure the same public origin is listed in `gateway.controlUi.allowedOrigins`
- if you use `gateway.auth.mode: "trusted-proxy"`, make sure identity headers are forwarded on both HTTP requests and WebSocket upgrades
- do not mix trusted-proxy mode with shared token auth unless upstream explicitly documents that combination as supported
- avoid same-host loopback reverse proxies for trusted-proxy mode; use token/password auth there instead
- make sure the tunnel or reverse proxy preserves the original host and scheme

If the page loads but OpenClaw reports origin rejection, check the exact public origin string and proxy forwarding rules first.

---

## Features

| Feature | Description |
|---|---|
| WebView2 Shell | Hosts the remote OpenClaw UI inside a native window |
| Environment Switching | Manage multiple hosted Control UI endpoints |
| Connection Status | Status bar, error InfoBar, retry support |
| Auto-Reconnect | Retries failed navigation automatically |
| Heartbeat | Periodic Control UI and transport probe with configurable reconnect thresholds |
| Session Isolation | Separate WebView2 profile data per configured environment |
| Theme | Top-bar segmented switcher for System, Light, and Dark |
| Language | English, Simplified Chinese, System |
| Diagnostics | Runtime, network, and session checks |
| Log Viewer | View today's log and open the log folder |
| DevTools | Open WebView2 developer tools |

---

## Settings

The Settings window is organized into four sections:

| Section | Content |
|---|---|
| Environments | Add, edit, remove, and choose default hosted Control UI endpoints |
| Language | Display language |
| Sessions | Clear WebView2 session data for a specific environment |
| Developer Tools | Diagnostics, logs, DevTools |

### Environment URL Rules

- Use the hosted Control UI page URL with `http://` or `https://`
- Do not use the raw Gateway WebSocket URL with `ws://` or `wss://`
- For Cloudflare Tunnel or reverse-proxy deployments, always use the exact public browser-facing origin

---

## Data Storage

All local data is stored under `%LOCALAPPDATA%\OpenClaw\`.

| Path | Content |
|---|---|
| `settings.json` | Environment configs, theme, language, heartbeat settings, window state |
| `logs/` | Daily log files |
| `WebView2Data/` | WebView2 profile data, cookies, cache |

---

## Architecture

```text
MainWindow
|- MainViewModel
|  |- ConfigurationService
|  `- LoggingService
`- WebViewService
```

Design principle: remote-first thin shell. The actual OpenClaw runtime lives on the VPS; this app is a native control surface for the hosted Control UI.

---

## Recent Changes

### v3.0.4 (2026-04-29)

- Fixed the main window top-edge artifact by removing the XAML edge cover workaround and explicitly syncing the WinUI title bar, DWM caption, and DWM border colors.
- Updated theme-change handling so `ActualThemeChanged` uses the full native frame refresh path instead of only repainting managed title-bar content.
- Synced app, assembly, file, manifest, and About dialog version metadata to `3.0.4`.

### v3.0.3 (2026-04-22)

- Kept the shell lightweight by narrowing Hosted UI DOM scanning to auth/origin/pairing/connectivity signals and avoiding broader page-text sweeps.
- Tuned the default heartbeat, reconnect, and hard-refresh cadence for the Cloudflare Tunnel remote-gateway path so the shell is less aggressive during transient tunnel jitter.
- Reduced startup and debug-session noise by removing eager string-resource warm-up, caching `CoreWebView2` handles, and de-duplicating high-frequency WebView lifecycle logs.

### v3.0.2 (2026-04-21)

- Fixed Visual Studio solution configuration mappings so the test project now maps cleanly across `x64`, `x86`, and `ARM64` solution platforms without showing unknown project configuration warnings.
- Reduced startup and background overhead by deferring non-critical warm-up work, pausing hidden-window activity, and tightening WebView recreation scheduling into a single debounced path.
- Added lightweight runtime observability for WebView recreation, Control UI inspect reuse/coalescing, deferred settings saves, and heartbeat-triggered recovery so diagnostics now expose the recent optimization paths more clearly.

### v3.0.1 (2026-04-21)

- Continued the refactor by splitting `MainWindow` and `SettingsDialog` startup logic into smaller initialization, action, navigation, and theme files without changing existing behavior.
- Consolidated duplicated window theme and title-bar refresh logic into shared helpers so the main window and settings window now follow the same theme-application pipeline.
- Fixed an initialization-order null reference in `ShellSessionCoordinator` by making logger and recovery-option dependencies available before `AttachAsync()` runs.
- Fixed the window-shell split so the new partial entry files compile cleanly and the main window, settings window, and About version display stay in sync at `3.0.1`.

### v3.0.0 (2026-04-21)

- Refactored shared window theme and native frame refresh logic into reusable helpers to reduce duplicate patch-style fixes across the main window and settings window.
- Split reusable command, indicator, and app metadata types out of large view model files to make responsibilities clearer and future maintenance safer.
- Consolidated main window environment selection and UI-thread update flows so behavior stays the same while the code path is easier to reason about.

### v2.1.4 (2026-04-20)

- Added a top-right latency badge for the active Control UI endpoint.
- Increased latency refresh cadence from 3 seconds to 1 second.
- Reduced transient blank latency readings by retaining the most recent successful ping value when a probe briefly misses.

### v2.1.3 (2026-04-20)

- Fixed the Settings window so reopening it immediately resyncs the current app theme before the window is shown again.
- Replaced the title bar refresh resize hack with a non-geometry non-client refresh path based on native frame invalidation, redraw, and DWM flush.

### v2.1.2 (2026-04-19)

- Unified heartbeat settings so runtime behavior now respects the configured enable flag and reconnect thresholds.
- Added settings normalization so legacy `heartbeatIntervalSeconds` values migrate cleanly into the newer heartbeat settings object.
- Added explicit disposal for `WebViewService`, `HostedUiBridge`, `ShellSessionCoordinator`, and main-window event subscriptions during shutdown.
- Improved diagnostics and settings guidance for Cloudflare Tunnel and reverse-proxy deployments, especially around `gateway.controlUi.allowedOrigins`.
- Clarified that environment URLs must use the public hosted Control UI page origin rather than the raw Gateway WebSocket endpoint.

### v2.0.9 (2026-03-31)

- Refined the recovery architecture so heartbeat, event-gap handling, and background resume all prefer in-page reconnect or soft resync before falling back to a hard reload.
- Added input-focus-aware recovery guards to reduce unexpected refreshes while typing.
- Removed the last dead duplicate bridge constant from `WebViewService`, leaving `HostedUiBridge` as the single injected page bridge.
- Polished the top status strip layout so heartbeat summary and indicators each occupy their own centered lane.
- Fixed the top heartbeat badge staying gray by preventing duplicate heartbeat restarts from resetting the timer before the first probe completed.
- Tightened the top status strip spacing so `HB`, `MODEL`, `AUTH`, and `Status` read more evenly without over-compressing the model label.

### v2.0.6 (2026-03-30)

- Consolidated hosted UI snapshot ownership under `WebViewService` and reduced duplicate status pipelines.
- Hardened WebView recreation and bridge reattachment behavior to avoid stale subscriptions.
- Localized heartbeat summary text in both English and Chinese.
