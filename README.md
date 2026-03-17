# OpenClaw Manager

**A lightweight Windows-native OpenClaw remote management shell built with WinUI 3 and WebView2.**

**基于 WinUI 3 + WebView2 的轻量级 Windows 原生 OpenClaw 远程管理客户端。**

---

## Overview / 概述

OpenClaw is a thin native shell around the existing OpenClaw Web UI, designed for users running OpenClaw Gateway remotely on a VPS and accessing it over Cloudflare Tunnel / reverse proxy / wss.

OpenClaw 是对现有 OpenClaw Web UI 的原生薄壳封装，专为在 VPS 上远程运行 OpenClaw Gateway 并通过 Cloudflare Tunnel / 反向代理 / wss 访问的用户设计。

### This is / 本项目是：
- A WinUI 3 + WebView2 remote management shell / WinUI 3 + WebView2 远程管理外壳
- A Windows-native entry point for OpenClaw remote control / OpenClaw 远程控制的 Windows 原生入口
- A thin client enhancing the existing Web UI with native UX / 增强现有 Web UI 原生体验的轻量客户端

### This is not / 本项目不是：
- A local Gateway or node host / 本地 Gateway 或节点
- A full native rewrite of the OpenClaw frontend / OpenClaw 前端的完整原生重写
- An offline-capable standalone app / 可离线运行的独立应用

---

## Tech Stack / 技术栈

| Component | Version |
|---|---|
| .NET | 10.0 |
| Windows App SDK | 1.8.x |
| WebView2 | Bundled via WinAppSDK |
| MVVM | CommunityToolkit.Mvvm 8.x |
| UI Language | English (i18n-ready) |
| Packaging | MSIX |

---

## Project Structure / 项目结构

```
Claw_winui3/
├── NuGet.config                         # NuGet package sources / NuGet 包源配置
├── OpenClaw.sln                  # Solution file / 解决方案文件
├── README.md                            # This file / 本文件
└── src/OpenClaw/
    ├── OpenClaw.csproj           # Project file (.NET 10 + WinAppSDK 1.8)
    ├── Package.appxmanifest             # MSIX packaging manifest / MSIX 打包清单
    ├── app.manifest                     # DPI awareness config / DPI 感知配置
    ├── App.xaml / App.xaml.cs           # Entry point, global services / 入口点，全局服务
    ├── MainWindow.xaml / .cs            # Shell: top bar + WebView2 + status bar / 主窗口
    ├── Assets/                          # App icons (placeholder) / 应用图标（占位符）
    ├── Strings/en-us/Resources.resw     # Centralized English strings / 集中管理的英文字符串
    ├── Abstractions/
    │   └── ShellAbstractions.cs         # Phase 5: upstream-compatible interfaces / 上游兼容接口
    ├── Models/
    │   ├── EnvironmentConfig.cs         # Gateway environment model / 网关环境模型
    │   └── AppSettings.cs               # Settings + JSON source gen / 设置 + JSON 序列化
    ├── Services/
    │   ├── ConfigurationService.cs      # JSON settings persistence / JSON 设置持久化
    │   ├── DiagnosticService.cs         # Phase 4: startup diagnostics / 启动诊断
    │   ├── LoggingService.cs            # Structured daily log files / 结构化日志（按日轮转）
    │   └── WebViewService.cs            # WebView2 lifecycle + JS injection / WebView2 生命周期
    ├── ViewModels/
    │   ├── MainViewModel.cs             # Main window commands + state / 主窗口命令与状态
    │   └── SettingsViewModel.cs         # Environment CRUD / 环境增删改
    ├── Views/
    │   ├── LogViewerDialog.xaml / .cs   # Phase 4: log viewer / 日志查看器
    │   └── SettingsDialog.xaml / .cs     # Environment management dialog / 环境管理对话框
    └── Helpers/
        └── StringResources.cs           # Typed .resw accessor / 类型化资源访问器
```

---

## Prerequisites / 前置要求

- **Windows 10 1809+** or **Windows 11**
- **Visual Studio 2026** with:
  - .NET Desktop Development workload / .NET 桌面开发工作负载
  - Windows App SDK C# Templates / Windows App SDK C# 模板
- **.NET 10 SDK** (verify: `dotnet --version` → `10.0.x`)
- **WebView2 Runtime** (built into Windows 11, or install from [Microsoft](https://developer.microsoft.com/en-us/microsoft-edge/webview2/))

---

## Getting Started / 快速开始

### Build & Run / 编译与运行

**Option A — Visual Studio (Recommended):**

```
1. Open OpenClaw.sln in Visual Studio 2026
   用 VS 2026 打开 OpenClaw.sln

2. Set Solution Platform to x64
   设置解决方案平台为 x64

3. Press F5 to deploy and run
   按 F5 部署并运行
```

**Option B — CLI:**

```powershell
# Restore packages / 恢复包
dotnet restore src\OpenClaw\OpenClaw.csproj

# Build / 编译
dotnet build src\OpenClaw\OpenClaw.csproj -p:Platform=x64
```

> **Note / 注意**: MSIX apps require deployment via Visual Studio for F5 launch. CLI build produces the DLL but does not deploy.  
> MSIX 应用需要通过 VS 部署才能运行，CLI 编译只生成 DLL 不会自动部署。

### First Launch / 首次启动

1. App opens with a default placeholder environment  
   应用以默认占位环境启动

2. Click the **⚙ Settings** button in the top bar  
   点击顶栏的 **⚙ 设置** 按钮

3. Add your OpenClaw Gateway URL (e.g., `https://your-gateway.example.com`)  
   添加你的 OpenClaw Gateway URL

4. Click **Save** → the WebView2 will load your remote OpenClaw UI  
   点击 **保存** → WebView2 将加载你的远程 OpenClaw UI

---

## Features / 功能

### Implemented (Phase 0–5) / 已实现

| Feature / 功能 | Phase | Description / 描述 |
|---|---|---|
| 🌐 WebView2 Shell | 1 | Load remote OpenClaw UI in embedded Chromium / 在内嵌 Chromium 中加载远程 UI |
| 🔄 Environment Switching | 1 | Manage multiple gateway environments (prod/test) / 管理多个网关环境 |
| 🔃 Reload | 1 | One-click page refresh (F5) / 一键刷新页面 |
| 🛑 Stop | 1 | JS injection to send `/stop` command (Esc) / JS 注入发送 `/stop` 命令 |
| ⚡ Quick Commands | 2 | Flyout: /stop, /status, /new, /queue / 快捷命令下拉菜单 |
| 📊 Connection Status | 2 | Real-time status bar + error InfoBar with retry / 实时状态栏 + 错误横幅 |
| 🔁 Auto-Reconnect | 2 | 3 retries with 3s delay on connection failure / 连接失败自动重试3次 |
| ⌨️ Keyboard Shortcuts | 2 | F5 Reload, Esc Stop, Ctrl+Shift+I DevTools, Ctrl+Shift+V Paste / 快捷键 |
| 📋 Clipboard Paste | 3 | Paste images from clipboard into remote UI / 从剪贴板粘贴图片到远程 UI |
| 📂 Drag & Drop | 3 | Drop files onto WebView2 for upload / 拖放文件到 WebView2 上传 |
| 📁 File Injection | 3 | Relay files via JS DataTransfer API / 通过 JS DataTransfer API 传递文件 |
| 🔍 Diagnostics | 4 | WebView2 runtime check, network probe, session detection / 运行时检查 |
| 📜 Log Viewer | 4 | View today's log with refresh + open-folder / 查看日志 |
| 🔧 DevTools | 2 | Open WebView2 developer tools / 打开开发者工具 |
| 🏗️ Abstractions | 5 | IWebViewHost, ICommandInjector, IFileRelay, etc. / 上游兼容接口 |

---

## Data Storage / 数据存储

All local data is stored under `%LOCALAPPDATA%\OpenClaw\`:

| Path | Content / 内容 |
|---|---|
| `settings.json` | Environment configs and window state / 环境配置与窗口状态 |
| `logs/` | Structured daily logs / 每日结构化日志 |
| `WebView2Data/` | WebView2 browser profile (cookies, cache) / WebView2 浏览器数据 |

---

## Architecture / 架构

```
┌─────────────────────────────────────────────┐
│  MainWindow (WinUI 3 Shell)                 │
│  ┌─────────────────────────────────────────┐│
│  │ Top Bar: Env Selector │ Reload │ Stop │ ││
│  ├─────────────────────────────────────────┤│
│  │                                         ││
│  │          WebView2 Control               ││
│  │    (Remote OpenClaw Web UI)             ││
│  │                                         ││
│  ├─────────────────────────────────────────┤│
│  │ Status Bar: ● Connected │ Production   ││
│  └─────────────────────────────────────────┘│
└─────────────────────────────────────────────┘
        │                │
   MainViewModel    WebViewService
        │                │
  ConfigurationService  LoggingService
        │
  settings.json (local)
```

**Design principle / 设计原则**: Remote-first thin shell. The real OpenClaw runtime lives on the VPS; this app is a control surface, not the brain.  
远程优先的轻量外壳。真正的 OpenClaw 运行时在 VPS 上，本应用只是控制界面。

---

## Development Progress / 开发进度

### ✅ Phase 0 — Setup & Feasibility / 环境搭建与可行性验证
*Completed 2026-03-12*

- [x] WinUI 3 project created targeting .NET 10 + WinAppSDK 1.8
- [x] Project builds successfully (0 errors)
- [x] WebView2 integration in place
- [x] Implementation plan approved

### ✅ Phase 1 — MVP Foundation / MVP 基础
*Completed 2026-03-12*

- [x] Main shell window (top bar + WebView2 + status bar)
- [x] Environment config model (JSON persistence)
- [x] Settings dialog (CRUD for environments)
- [x] Reload, Stop, Open in Browser, Clear Session
- [x] Connection status display
- [x] Window size/position memory
- [x] Centralized string resources (.resw)
- [x] Structured logging

### ✅ Phase 2 — Native Management Enhancements / 原生管理增强
*Completed 2026-03-12*

- [x] Quick commands flyout (/stop, /status, /new, /queue)
- [x] Connection state InfoBar with retry button
- [x] Auto-reconnect on failure (3 retries, 3s delay)
- [x] Keyboard shortcuts (F5 Reload, Esc Stop, Ctrl+Shift+I DevTools)
- [x] DevTools button

### ✅ Phase 3 — File and Image Handling / 文件与图片处理
*Completed 2026-03-12*

- [x] Clipboard image paste bridge (Ctrl+Shift+V)
- [x] Drag-and-drop file upload relay
- [x] File injection via JS DataTransfer API
- [x] MIME type detection for common file types

### ✅ Phase 4 — Stability & Logging / 稳定性与日志
*Completed 2026-03-12*

- [x] DiagnosticService (WebView2 runtime, network probe, session check)
- [x] Diagnostics InfoBar with results
- [x] WebView init error handling
- [x] Auth/session invalidation detection
- [x] Log viewer dialog (monospace, refresh, open-folder)

### ✅ Phase 5 — Upstream Compatibility / 上游兼容性
*Completed 2026-03-12*

- [x] IRemoteEnvironment, IWebViewHost, ICommandInjector
- [x] IFileRelay, IDiagnosticRunner, IConfigurationStore
- [x] HostConnectionState enum (decoupled)
- [x] ShellAbstractions.cs in Abstractions namespace

---

## License / 许可

TBD

---

*Last updated / 最后更新: 2026-03-12 (Phase 5 — All phases complete)*
