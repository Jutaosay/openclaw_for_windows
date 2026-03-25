# OpenClaw Manager

Lightweight Windows-native OpenClaw remote management shell built with WinUI 3 and WebView2.

基于 WinUI 3 与 WebView2 的轻量级 Windows 原生 OpenClaw 远程管理客户端。

---

## Overview / 概述

OpenClaw Manager is a thin native shell around the existing OpenClaw Web UI. It is designed for users who run OpenClaw Gateway remotely on a VPS and access it through Cloudflare Tunnel, reverse proxy, or WSS.

OpenClaw Manager 是对现有 OpenClaw Web UI 的原生薄壳封装，适合将 OpenClaw Gateway 部署在 VPS 上并通过 Cloudflare Tunnel、反向代理或 WSS 远程访问的场景。

### This project is / 本项目是

- A WinUI 3 + WebView2 remote management shell
- A Windows-native entry point for OpenClaw remote control
- A thin client that enhances the existing Web UI with native UX

### This project is not / 本项目不是

- A local Gateway or node host
- A full native rewrite of the OpenClaw frontend
- An offline-capable standalone application

---

## Tech Stack / 技术栈

| Component | Version |
|---|---|
| .NET | 10.0 |
| Windows App SDK | 1.8.x |
| WebView2 | Bundled via WinAppSDK |
| MVVM | CommunityToolkit.Mvvm 8.x |
| UI Language | English + 中文 |
| Packaging | Unpackaged, self-contained |

---

## Project Structure / 项目结构

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

### Key folders / 主要目录

- `Services/`: configuration, logging, diagnostics, WebView2 lifecycle
- `ViewModels/`: shell state, commands, settings editing
- `Views/`: settings, about, log viewer dialogs
- `Strings/`: localized UI resources

---

## Runtime Requirements / 运行环境

The compiled application requires the following:

程序运行需要以下组件：

| Dependency | Download |
|---|---|
| .NET 10 Desktop Runtime | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| WebView2 Runtime | [Download](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) |

Note: Windows 11 usually already includes WebView2 Runtime. Windows 10 users may need to install it manually.

注：Windows 11 通常已自带 WebView2 Runtime，Windows 10 用户可能需要手动安装。

---

## Development Prerequisites / 开发前置要求

- Windows 10 1809+ or Windows 11
- Visual Studio 2026
- .NET Desktop Development workload
- Windows App SDK C# templates
- .NET 10 SDK

---

## Getting Started / 快速开始

### Visual Studio

1. Open [OpenClaw.sln](/C:/Users/Zen/Repo/Codings/Claw_winui3/OpenClaw.sln) in Visual Studio 2026.
2. Set solution platform to `x64`.
3. Press `F5` to run.

### CLI

```powershell
dotnet restore src\OpenClaw\OpenClaw.csproj
dotnet build src\OpenClaw\OpenClaw.csproj -r win-x64
```

### First Launch / 首次启动

1. The app starts with a placeholder environment.
2. Open Settings from the top bar.
3. Add your OpenClaw Gateway URL, for example `https://your-gateway.example.com`.
4. Save settings and the embedded WebView2 shell will load the remote UI.

---

## Features / 功能

| Feature | Description |
|---|---|
| WebView2 Shell | Hosts the remote OpenClaw UI inside a native window |
| Environment Switching | Manage multiple gateway endpoints |
| Connection Status | Status bar, error InfoBar, retry support |
| Auto-Reconnect | Retries failed navigation automatically |
| Heartbeat | Periodic gateway probe with reconnect on repeated failures |
| Theme | Top-bar segmented switcher for System, Light, and Dark |
| Language | English, 中文, System |
| Diagnostics | Runtime, network, and session checks |
| Log Viewer | View today's log and open the log folder |
| DevTools | Open WebView2 developer tools |

---

## Settings / 设置

The Settings window is organized into three sections:

设置窗口分为三个分区：

| Section | Content |
|---|---|
| Environments | Add, edit, remove, and choose default gateway environments |
| Language | Display language |
| Developer Tools | Diagnostics, logs, DevTools, clear session |

---

## Data Storage / 数据存储

All local data is stored under `%LOCALAPPDATA%\OpenClaw\`.

所有本地数据存储在 `%LOCALAPPDATA%\OpenClaw\` 下。

| Path | Content |
|---|---|
| `settings.json` | Environment configs, theme, language, window state |
| `logs/` | Daily log files |
| `WebView2Data/` | WebView2 profile data, cookies, cache |

---

## Architecture / 架构

```text
MainWindow
|- MainViewModel
|  |- ConfigurationService
|  `- LoggingService
`- WebViewService
```

Design principle: remote-first thin shell. The actual OpenClaw runtime lives on the VPS; this app is a native control surface.

设计原则：远程优先的轻量外壳。真正的 OpenClaw 运行时部署在 VPS 上，本应用只负责提供原生控制界面。

---

## Recent Changes / 最近更新

### v1.0.4 (2026-03-25)

- Replaced the application and shell branding icons with the OpenClaw logo for a more consistent visual identity.
- Kept the dark-titlebar edge fix stable after icon and title bar polish changes.

### v1.0.3 (2026-03-25)

- Fixed the dark-titlebar top-edge white line by strengthening native window frame refresh when switching from Light to Dark.
- Stabilized custom title bar rendering so the issue no longer reappears after the initial dark-mode transition.

### v1.0.2 (2026-03-25)

- Fixed language fallback so switching back to `System` correctly clears the explicit language override.
- Fixed selected-environment persistence when an environment is renamed in Settings.
- Cleaned up several user-facing text encoding issues in diagnostics and shell UI.
- Hardened a few view-model bindings used by the main shell window.
- Removed rarely used top-bar actions for reload, stop, and quick commands to simplify the shell UI.
- Moved theme switching back to the top bar for direct one-click access.

### v1.0.1 (2026-03-22)

- Added heartbeat probing with automatic reconnect after repeated failures.
- Added `HeartbeatIntervalSeconds` setting, default `30`, `0` disables heartbeat.
- Added heartbeat failure status messaging.

### v1.0.0 (2026-03-19)

- First stable release.
- Improved language settings layout.
- Expanded runtime requirement documentation.

---

## License / 许可

TBD

---

Developed by [@Jutaosay](https://github.com/Jutaosay) · [GitHub Repository](https://github.com/Jutaosay/openclaw_for_windows)

Current version: 1.0.4

Last updated: 2026-03-25
