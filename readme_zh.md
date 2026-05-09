# OpenClaw Manager 简体中文

**语言：** [English](README.md) | 简体中文

OpenClaw Manager 是一个轻量的 Windows 原生 OpenClaw 远程管理外壳，基于 WinUI 3 和 WebView2 构建。

OpenClaw Manager 是托管版 OpenClaw Control UI 的薄桌面外壳。它面向运行在 VPS 上、并通过 Cloudflare Tunnel、反向代理或其他公共 HTTPS 源暴露的远程 Gateway 部署。

---

## 概览

本项目保留现有 OpenClaw Web 体验，同时把它包进一个原生 WinUI 3 窗口，并提供：

- 环境切换
- 每个环境独立的 WebView2 会话隔离
- 连接恢复和心跳监控
- 诊断和结构化日志
- 原生主题和窗口集成

它适合以下用户：

- 在远程机器上运行 OpenClaw Gateway
- 通过 Cloudflare Tunnel 或反向代理访问它
- 想用轻量 Windows 原生客户端，而不是一直开着浏览器标签页

### 本项目是

- WinUI 3 + WebView2 远程管理外壳
- 托管 OpenClaw Control UI 会话的 Windows 原生入口
- 在现有 Web UI 之上增强原生 UX 的薄客户端

### 本项目不是

- 本地 Gateway 或节点宿主
- OpenClaw 前端的完整原生重写
- 可离线使用的独立应用

---

## 技术栈

| 组件 | 版本 |
|---|---|
| .NET | 10.0 |
| Windows App SDK | 1.8.x |
| WebView2 | 通过 WinAppSDK 捆绑 |
| MVVM | CommunityToolkit.Mvvm 8.x |
| UI 语言 | 英文 + 简体中文 |
| 打包方式 | Unpackaged, self-contained |

---

## 项目结构

```text
Claw_winui3/
|-- NuGet.config
|-- OpenClaw.sln
|-- DEVELOPMENT_NOTES.md
|-- README.md
|-- readme_zh.md
|-- src/
|   |-- OpenClaw/
|   |   |-- OpenClaw.csproj
|   |   |-- Package.appxmanifest
|   |   |-- app.manifest
|   |   |-- App.xaml
|   |   |-- App.xaml.cs
|   |   |-- MainWindow.xaml
|   |   |-- MainWindow.xaml.cs
|   |   |-- Assets/
|   |   |-- Abstractions/
|   |   |-- Helpers/
|   |   |-- Models/
|   |   |-- Services/
|   |   |-- Strings/
|   |   |-- ViewModels/
|   |   `-- Views/
|   `-- OpenClaw.Core/
|       `-- OpenClaw.Core.csproj
`-- tests/OpenClaw.Tests/
    |-- OpenClaw.Tests.csproj
    `-- Program.cs
```

### 关键目录

- `Services/`：配置、日志、诊断、WebView2 生命周期和恢复辅助逻辑
- `OpenClaw.Core/`：可被测试项目复用的纯 .NET 共享代码
- `tests/OpenClaw.Tests/`：覆盖恢复、设置、托盘、版本元数据和持久化行为的轻量回归测试
- `ViewModels/`：外壳状态、命令和设置编辑
- `Views/`：设置、关于和日志查看对话框
- `Strings/`：本地化 UI 资源

---

## 运行时要求

编译后的应用需要：

| 依赖 | 下载 |
|---|---|
| .NET 10 Desktop Runtime | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| WebView2 Runtime | [Download](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) |

Windows 11 通常已经内置 WebView2 Runtime。Windows 10 用户可能需要手动安装。

---

## 开发前置条件

- Windows 10 1809+ 或 Windows 11
- Visual Studio 2026
- .NET Desktop Development workload
- Windows App SDK C# templates
- .NET 10 SDK

### 依赖恢复说明

- 解决方案使用 SDK-style 项目和 `PackageReference`。
- 仓库本地的 `packages/` 文件夹不是必需的，可以安全删除。
- 解决方案级 [Directory.Build.props](Directory.Build.props) 启用 `RestorePackagesConfig=true`，因此如果未来加入 `packages.config` 项目，完整 Visual Studio / MSBuild 构建仍会自动恢复依赖。
- 清理本地缓存后的预期流程是：

```powershell
dotnet restore OpenClaw.sln
dotnet build OpenClaw.sln
```

### 开发日志

参见 [DEVELOPMENT_NOTES.md](DEVELOPMENT_NOTES.md)，了解原生窗口 chrome、主题同步和其他维护敏感区域的经验记录。

---

## 快速开始

### Visual Studio

1. 用 Visual Studio 2026 打开 [OpenClaw.sln](OpenClaw.sln)。
2. 将解决方案平台设为 `x64`。
3. 按 `F5` 运行。

### CLI

```powershell
dotnet restore src\OpenClaw\OpenClaw.csproj
dotnet build src\OpenClaw\OpenClaw.csproj -r win-x64
```

### 首次启动

1. 应用会使用一个占位环境启动。
2. 从顶部栏打开 Settings。
3. 添加你的公共 OpenClaw Control UI URL，例如 `https://your-gateway.example.com`。
4. 保存设置后，内嵌 WebView2 外壳会加载远程 UI。

### Cloudflare Tunnel / VPS 说明

如果你的 OpenClaw Gateway 运行在 VPS 上并位于 Cloudflare Tunnel 后面：

- 在 OpenClaw Manager 中使用公共 HTTPS Control UI URL
- 不要使用原始 Gateway WebSocket URL
- 确保同一个公共 origin 已列入 `gateway.controlUi.allowedOrigins`
- 如果使用 `gateway.auth.mode: "trusted-proxy"`，确保 HTTP 请求和 WebSocket upgrade 都转发身份头
- 除非上游明确说明支持，不要混用 trusted-proxy 模式和 shared token auth
- trusted-proxy 模式下避免同主机 loopback 反向代理；这种场景请改用 token/password auth
- 确保 tunnel 或反向代理保留原始 host 和 scheme

如果页面可以加载但 OpenClaw 报 origin rejection，先检查精确的公共 origin 字符串和代理转发规则。

---

## 功能

| 功能 | 说明 |
|---|---|
| WebView2 Shell | 在原生窗口内托管远程 OpenClaw UI |
| Environment Switching | 管理多个托管 Control UI 端点 |
| Connection Status | 状态栏、错误 InfoBar 和重试支持 |
| Auto-Reconnect | 导航失败后自动重试 |
| Heartbeat | 周期性 Control UI 和 transport 探测，支持可配置重连阈值 |
| System Tray | 可配置的最小化/关闭到托盘行为，提供 Open OpenClaw、Settings 和 Exit 操作 |
| Instance Control | 可选多实例模式；默认关闭，重新启动会恢复已有托盘隐藏窗口 |
| Session Isolation | 每个配置环境使用独立 WebView2 profile 数据 |
| Latency Tooltip | 悬停延迟徽标查看最新、最小、平均、p95 和最大往返时间 |
| Theme | 顶部栏 System、Light、Dark 分段切换 |
| Language | English、Simplified Chinese、System |
| Diagnostics | 运行时、网络和会话检查 |
| Log Viewer | 查看当天日志并打开日志目录 |
| DevTools | 打开 WebView2 developer tools |

---

## 设置

Settings 窗口包含五个部分：

| 部分 | 内容 |
|---|---|
| Language | 显示语言 |
| Environments | 添加、编辑、删除和选择默认托管 Control UI 端点 |
| Sessions | 清理指定环境的 WebView2 会话数据 |
| Developer Tools | 诊断、日志、DevTools |
| Advanced | 最小化到托盘、关闭到托盘和多实例行为 |

### 环境 URL 规则

- 使用带 `http://` 或 `https://` 的托管 Control UI 页面 URL
- 不要使用 `ws://` 或 `wss://` 的原始 Gateway WebSocket URL
- 对 Cloudflare Tunnel 或反向代理部署，始终使用浏览器可访问的精确公共 origin

---

## 数据存储

所有本地数据都存储在 `%LOCALAPPDATA%\OpenClaw\` 下。

| 路径 | 内容 |
|---|---|
| `settings.json` | 环境配置、主题、语言、托盘和实例行为、心跳设置、窗口状态 |
| `logs/` | 每日日志文件 |
| `WebView2Data/` | WebView2 profile 数据、cookies、缓存 |

---

## 架构

```text
MainWindow
|- MainViewModel
|  |- ConfigurationService
|  `- LoggingService
`- WebViewService
```

设计原则：remote-first thin shell。真正的 OpenClaw runtime 位于 VPS 上；本应用是托管 Control UI 的原生控制面。

---

## 最近更新

### v3.1.3 (2026-05-08)

- 修复最小化到托盘后通过任务栏或系统 restore 恢复时的窗口异常，隐藏窗口前会先恢复 minimized HWND placement。
- 覆盖独显直连模式下剩余的 restore 路径，避免 Windows 在任务栏激活后仍把主窗口保持在 `160x28` 和 `-32000,-32000`。
- 增加回归测试，确保托盘隐藏逻辑在调用 `SW_HIDE` 前先恢复 minimized placement。
- 同步 app、assembly、file、package manifest、application manifest 和 About dialog 版本元数据到 `3.1.3`。

### v3.1.2 (2026-05-08)

- 修复 GPU/显示拓扑变化后主窗口恢复问题，例如切换到独显直连模式。
- 清理持久化的最小化窗口哨兵 bounds，例如 `160x28` 和 `-32000,-32000`，启动时回退到可见默认窗口。
- 主窗口隐藏到托盘或最小化时停止保存窗口 bounds，避免再次持久化不可见窗口状态。
- 当已保存窗口矩形不再与任何可用工作区相交时，将窗口重新居中到当前显示器。
- 同步 app、assembly、file、package manifest、application manifest 和 About dialog 版本元数据到 `3.1.2`。

### v3.1.1 (2026-05-02)

- 将 Settings 的 More 区域重命名为 Advanced。
- 同步 app、assembly、file、package manifest、application manifest 和 About dialog 版本元数据到 `3.1.1`。

### v3.1.0 (2026-05-02)

- 添加系统托盘图标、状态 tooltip、最小化/关闭到托盘支持，以及右键 Open OpenClaw、Settings、Exit 操作。
- 通过为 Win32 `*W` 入口声明 Unicode marshalling 修复托盘初始化，包括窗口类注册、图标加载和菜单文本。
- 通过从 `LOWORD(lParam)` 读取事件修复 `NOTIFYICON_VERSION_4` 回调格式下的托盘右键处理。
- 通过使用隐藏的普通 owner window 而非 message-only `HWND_MESSAGE` 修复托盘菜单弹出行为。
- 添加 More 设置，用于最小化到托盘、关闭到托盘和可选多实例行为。
- 默认禁用多实例；关闭该设置时，二次启动会恢复已有 OpenClaw 窗口。
- 将 Shell 设置区域重命名为 More 并移动到设置导航底部。
- 在启用且托盘图标可用时，将窗口最小化和关闭行为改为隐藏到托盘。
- 为延迟徽标添加悬停详情，展示最近探测样本的 latest、min、average、p95 和 max 往返时间。
- 同步 app、assembly、file、manifest 和 About dialog 版本元数据到 `3.1.0`。

### v3.0.6 (2026-05-02)

- 修复 deferred settings save，使前一次写入 flush 时排队的更新会由后续 save 持久化。
- 加固 settings load，处理 environments、heartbeat、recovery 和 diagnostics options 显式为 `null` 的 JSON。
- 将日志保留清理从 `LoggingService` 构造路径移到后台 writer task。
- 将延迟探测切换为在配置的 Control UI base path 下请求 `GET __openclaw/control-ui-config.json`，并干净取消初始探测任务。
- 将纯 .NET recovery/config/logging 代码拆入 `OpenClaw.Core`，让测试可以引用真实共享代码。
- 固定 NuGet 包版本、启用 package lock files，并移除过时的 `RestorePackagesConfig` restore 开关。
- 同步 app、assembly、file、manifest 和 About dialog 版本元数据到 `3.0.6`。

### v3.0.5 (2026-05-01)

- 使用原子写入加固 settings persistence，避免中断保存留下截断的 `settings.json`。
- 用 HTTP HEAD RTT 探测替代 ICMP 延迟检查，并在 heartbeat recovery 中遵守 hard-refresh cooldown，改善 Cloudflare Tunnel 行为。
- 通过显式关闭被替换的 WebView2 实例、日志查看器 tail-read 和 14 天日志保留，减少本地资源堆积。
- 通过去重 heartbeat/run indicator 属性变更，并将 Stop 命令路径改为可 await 的异步执行，减少 UI 抖动。
- 同步 app、assembly、file、manifest 和 About dialog 版本元数据到 `3.0.5`。

### v3.0.4 (2026-04-29)

- 移除 XAML edge cover workaround，并显式同步 WinUI title bar、DWM caption 和 DWM border 颜色，修复主窗口顶部边缘伪影。
- 更新主题切换处理，使 `ActualThemeChanged` 走完整 native frame refresh 路径，而不是只重绘 managed title-bar content。
- 同步 app、assembly、file、manifest 和 About dialog 版本元数据到 `3.0.4`。

### v3.0.3 (2026-04-22)

- 将 Hosted UI DOM 扫描收窄到 auth/origin/pairing/connectivity 信号，避免更宽泛的页面文本扫描，保持外壳轻量。
- 调整默认 heartbeat、reconnect 和 hard-refresh 节奏，使 Cloudflare Tunnel 远程 Gateway 路径在瞬时 tunnel 抖动时不那么激进。
- 移除 eager string-resource warm-up、缓存 `CoreWebView2` 句柄，并去重高频 WebView 生命周期日志，减少启动和调试噪音。

### v3.0.2 (2026-04-21)

- 修复 Visual Studio 解决方案配置映射，使测试项目在 `x64`、`x86` 和 `ARM64` 平台下不再显示 unknown project configuration 警告。
- 通过延迟非关键 warm-up、暂停 hidden-window activity，并将 WebView recreation scheduling 收敛为单一 debounce 路径，降低启动和后台开销。
- 为 WebView recreation、Control UI inspect reuse/coalescing、deferred settings save 和 heartbeat-triggered recovery 增加轻量运行时可观测性。

### v3.0.1 (2026-04-21)

- 继续重构，将 `MainWindow` 和 `SettingsDialog` 启动逻辑拆为更小的 initialization、action、navigation 和 theme 文件，不改变现有行为。
- 将重复的窗口主题和 title-bar refresh 逻辑合并到共享 helpers，使主窗口和设置窗口使用同一主题应用 pipeline。
- 通过让 logger 和 recovery-option dependencies 在 `AttachAsync()` 之前可用，修复 `ShellSessionCoordinator` 初始化顺序空引用。
- 修复 window-shell 拆分后的编译问题，确保主窗口、设置窗口和 About version display 在 `3.0.1` 保持同步。

### v3.0.0 (2026-04-21)

- 将共享窗口主题和 native frame refresh 逻辑重构为可复用 helpers，减少主窗口和设置窗口间重复的 patch-style 修复。
- 拆出可复用 command、indicator 和 app metadata 类型，让职责更清晰，后续维护更安全。
- 合并主窗口环境选择和 UI-thread update 流程，在行为不变的前提下让代码路径更易推理。

### v2.1.4 (2026-04-20)

- 为当前 Control UI 端点添加右上角延迟徽标。
- 将延迟刷新频率从 3 秒提高到 1 秒。
- 探测短暂失败时保留最近一次成功 ping 值，减少临时空白延迟读数。

### v2.1.3 (2026-04-20)

- 修复 Settings 窗口，使重新打开时会在显示前立即同步当前 app theme。
- 用基于 native frame invalidation、redraw 和 DWM flush 的非几何 non-client refresh 路径替代 title bar refresh resize hack。

### v2.1.2 (2026-04-19)

- 统一 heartbeat settings，使运行时行为遵守配置的 enable flag 和 reconnect thresholds。
- 添加 settings normalization，使旧的 `heartbeatIntervalSeconds` 值能干净迁移到新的 heartbeat settings object。
- 在关闭时显式释放 `WebViewService`、`HostedUiBridge`、`ShellSessionCoordinator` 和主窗口事件订阅。
- 改进 Cloudflare Tunnel 和反向代理部署的诊断与设置说明，尤其是 `gateway.controlUi.allowedOrigins`。
- 明确环境 URL 必须使用公共托管 Control UI 页面 origin，而不是原始 Gateway WebSocket endpoint。

### v2.0.9 (2026-03-31)

- 调整 recovery 架构，使 heartbeat、event-gap handling 和 background resume 都优先尝试 in-page reconnect 或 soft resync，再回退到 hard reload。
- 添加 input-focus-aware recovery guards，减少输入时的意外刷新。
- 移除 `WebViewService` 中最后一个无用重复 bridge constant，让 `HostedUiBridge` 成为唯一注入页面 bridge。
- 打磨顶部状态条布局，让 heartbeat summary 和 indicators 各自占据居中的独立 lane。
- 修复顶部 heartbeat badge 一直为灰色的问题，避免重复 heartbeat restart 在第一次探测完成前重置 timer。
- 收紧顶部状态条间距，让 `HB`、`MODEL`、`AUTH` 和 `Status` 更均衡，同时不过度压缩 model label。

### v2.0.6 (2026-03-30)

- 将 hosted UI snapshot ownership 合并到 `WebViewService`，减少重复状态 pipeline。
- 加固 WebView recreation 和 bridge reattachment 行为，避免 stale subscriptions。
- 本地化英文和中文 heartbeat summary 文本。
