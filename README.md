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

### v2.1.0 (2026-04-19)

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
