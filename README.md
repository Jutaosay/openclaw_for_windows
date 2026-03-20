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
| UI Language | English + 中文 (i18n-ready) |
| Packaging | Unpackaged (self-contained) |

---

## Project Structure / 项目结构

```
Claw_winui3/
├── NuGet.config                         # NuGet package sources / NuGet 包源配置
├── OpenClaw.sln                  # Solution file / 解决方案文件
├── README.md                            # This file / 本文件
└── src/OpenClaw/
    ├── OpenClaw.csproj           # Project file (.NET 10 + WinAppSDK 1.8)
    ├── Package.appxmanifest             # App manifest / 应用清单
    ├── app.manifest                     # DPI awareness config / DPI 感知配置
    ├── App.xaml / App.xaml.cs           # Entry point, global services / 入口点，全局服务
    ├── MainWindow.xaml / .cs            # Shell: top bar + WebView2 + status bar / 主窗口
    ├── Assets/                          # App icons (placeholder) / 应用图标（占位符）
    ├── Strings/en-us/Resources.resw     # Centralized English strings / 集中管理的英文字符串
    ├── Abstractions/
    │   └── ShellAbstractions.cs         # Upstream-compatible interfaces / 上游兼容接口
    ├── Models/
    │   ├── EnvironmentConfig.cs         # Gateway environment model / 网关环境模型
    │   └── AppSettings.cs               # Settings + JSON source gen / 设置 + JSON 序列化
    ├── Services/
    │   ├── ConfigurationService.cs      # JSON settings persistence / JSON 设置持久化
    │   ├── DiagnosticService.cs         # Startup diagnostics / 启动诊断
    │   ├── LoggingService.cs            # Structured daily log files / 结构化日志（按日轮转）
    │   └── WebViewService.cs            # WebView2 lifecycle + JS injection / WebView2 生命周期
    ├── ViewModels/
    │   ├── MainViewModel.cs             # Main window commands + state / 主窗口命令与状态
    │   └── SettingsViewModel.cs         # Settings CRUD + theme / 设置管理与主题
    ├── Views/
    │   ├── AboutDialog.xaml / .cs       # About dialog / 关于对话框
    │   ├── LogViewerDialog.xaml / .cs   # Log viewer / 日志查看器
    │   └── SettingsDialog.xaml / .cs     # Unified settings (3 sections) / 统一设置页面
    └── Helpers/
        └── StringResources.cs           # Typed .resw accessor / 类型化资源访问器
```

---

## Runtime Requirements / 运行环境

运行编译后的程序需要以下组件 / The following are required to run the compiled application:

| Dependency | Download |
|---|---|
| **.NET 10 Desktop Runtime** | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) (选择 "Desktop Runtime" / Choose "Desktop Runtime") |
| **WebView2 Runtime** | [Download](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (Windows 11 已内置 / Built into Windows 11) |

> **Note / 注意**: Windows 11 已自带 WebView2 Runtime，无需额外安装。Windows 10 用户需手动安装。

---

## Prerequisites (Development) / 开发前置要求

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
dotnet build src\OpenClaw\OpenClaw.csproj -r win-x64
```

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

| Feature / 功能 | Description / 描述 |
|---|---|
| 🌐 WebView2 Shell | Load remote OpenClaw UI in embedded Chromium / 在内嵌 Chromium 中加载远程 UI |
| 🔄 Environment Switching | Manage multiple gateway environments (prod/test) / 管理多个网关环境 |
| 🔃 Reload | One-click page refresh (F5) / 一键刷新页面 |
| 🛑 Stop | JS injection to send `/stop` command (Esc) / JS 注入发送 `/stop` 命令 |
| ⚡ Quick Commands | Flyout: /stop, /status, /new, /queue / 快捷命令下拉菜单 |
| 📊 Connection Status | Real-time status bar + error InfoBar with retry / 实时状态栏 + 错误横幅 |
| 🔁 Auto-Reconnect | 3 retries with 3s delay on connection failure / 连接失败自动重试3次 |
| 🎨 Theme Switcher | Light / Dark / System theme in Settings / 设置中切换亮色/暗色/跟随系统主题 |
| 🌐 Language | English / 中文 in Settings / 设置中切换英文/中文界面 |
| 🔍 Diagnostics | WebView2 runtime check, network probe, session detection / 运行时检查 |
| 📜 Log Viewer | View today's log with refresh + open-folder / 查看日志 |
| 🔧 DevTools | Open WebView2 developer tools / 打开开发者工具 |
| 🏗️ Abstractions | IWebViewHost, ICommandInjector, IDiagnosticRunner, etc. / 上游兼容接口 |

---

## Settings / 设置页面

v0.2.0 起，所有设置统一在 Settings 对话框中管理，分为 3 个卡片区域：

| Section / 分区 | Content / 内容 |
|---|---|
| **Gateway Environments** | CRUD 管理网关环境（名称、URL、默认标记） |
| **Appearance** | 主题切换 (Light/Dark/System) + 语言切换 (English/中文) |
| **Developer Tools** | Run Diagnostics, View Logs, Open DevTools, Clear Session |

---

## Data Storage / 数据存储

All local data is stored under `%LOCALAPPDATA%\OpenClaw\`:

| Path | Content / 内容 |
|---|---|
| `settings.json` | Environment configs, theme, and window state / 环境配置、主题与窗口状态 |
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

## Changelog / 更新日志

### v1.0.0 (2026-03-19)

- 🎉 **正式发布**
- Language 单独放到侧栏导航，布局与 Appearance 统一
- Runtime Requirements 文档补充

### v0.3.0 (2026-03-19)

- **多语言支持**：Settings > Appearance 新增 Language 切换（English / 中文 / System Default）
- 新增 `Strings/zh-cn/Resources.resw` 全量中文翻译
- 启动时自动应用上次选择的语言偏好
- 版本号升级至 v0.3.0

### v0.2.0 (2026-03-19)

- **Settings 页面统一重构**：3 分区卡片布局（Environments / Appearance / Developer Tools）
- **主题切换移入 Settings**：支持 Light / Dark / System Default
- **移除调试功能**：Paste Image、Open in Browser、Drag-Drop 文件上传
- **代码清理**：移除 IFileRelay 接口、精简 WebViewService 和 MainViewModel

### v0.1.0 (2026-03-12)

- 初始版本：WinUI 3 + WebView2 远程管理外壳
- 环境管理、Quick Commands、连接状态监控、Auto-Reconnect
- Diagnostics、Log Viewer、DevTools
- 上游兼容抽象层

---

## License / 许可

TBD

---

*Developed by [@Jutaosay](https://github.com/Jutaosay) · [GitHub Repository](https://github.com/Jutaosay/openclaw_for_windows)*

*Last updated / 最后更新: 2026-03-19 (v1.0.0)*
