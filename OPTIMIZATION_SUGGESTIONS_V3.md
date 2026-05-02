# OpenClaw Manager 功能扩展建议 V3

**版本**：v3.0.6（在 V2 评审 v3.0.5 之后又做了一轮架构落地）
**与 V1 / V2 的差别**：V1 / V2 是"修代码质量"清单，V3 转向**"加什么新功能"**——这一轮 Codex 已经把 V1 / V2 列出的关键 22 项里的大部分技术债清掉了（拆 `OpenClaw.Core`、pin NuGet、原子写、HEAD → `__openclaw/control-ui-config.json`、retention 移到 background writer、`IAppLogger` 抽象等），架构已经稳到可以承载新功能。

---

## 0. 进入 V3 时项目处于的位置（一句话）

OpenClaw Manager 现在是一个**可靠的、对 Cloudflare Tunnel 友好的、可观测的远端 Control UI 容器**：你可以信任它在断网/重连/auth 边角不会"假死"，状态栏有有意义的延迟/心跳数据，崩溃了能从日志找到原因，settings.json 不会被写坏，测试能验证 recovery 状态机。

**但作为一个"native shell"**——也就是说，当 Web 版 Control UI 已经能用浏览器打开时，凭什么用户要装这个 WinUI 3 客户端？目前的回答是"原生主题 + 多环境 + 心跳 + 启动快"。这些是**门槛**，不是**护城河**。下面 V3 的功能建议都瞄准一个目标：**让原生壳给出 Web 浏览器无法提供的体验**，否则用户会回去用 Cloudflare Tunnel 直连浏览器。

---

## 1. 刚需梯度（v3 重排）

> 评判标准——**没它的时候用户的实际后果是什么**。如果用户能找到自然 workaround 不感到摩擦，就不算刚需。F3 已经按上一轮讨论降级。

| 阶段 | 功能 | 刚需等级 | 没它时用户的实际处境 | 估算 |
|---|---|---|---|---|
| **0. 让原生壳立得住的最小集**（不做就没有理由装这个壳） | | | | |
| | F1 托盘 + 全局热键 | 🟥 刚需 | 跟普通窗口一样要 Alt-Tab 找——直接退化为"另一个浏览器 tab"，原生壳的差异化荡然无存 | ~5 工程日 |
| | F4 Velopack 自动更新 | 🟥 刚需 | 两周 7 个版本、用户每次手动去 Release 替换——绝大多数人会停留在第一次安装的版本，BUG 修了等于没修 | ~3-4 工程日 |
| **1. 让长任务工作流真正可用**（这是远程 AI 助手最高频场景） | | | | |
| | F6 紧凑 / Always-on-Top | 🟧 准刚需 | 用户改去用 FancyZones 手动布局——能凑合，但每次开机都要重新调；这是最常被外置工具替代的功能 | ~3 工程日 |
| | F2 完成 toast 通知 | 🟧 准刚需 | 用户提交长任务后只能"反复切回来看进度"或者全靠 F6 把窗口放在视野里。两条都行，做了 F6 之后 F2 边际收益变小 | ~3 工程日 |
| **2. 出问题时刚需，平时不调用**（先不做不痛，第一次有人来报 bug 才补） | | | | |
| | F5 诊断包导出 | 🟨 条件刚需 | 用户报 bug → 截图 + 翻 settings + 找 log → 90% 的人不会做完，bug 报告全是"它就是不工作"。**第一次社区有人 ping issue 时立刻补**。 | ~2 工程日 |
| | F3-revised: latency hover tooltip<br>(min/avg/p95/max，**不画图**) | 🟨 条件刚需 | 工程师查"刚才网络抖了一下吗"用，纯文本数字够。原 F3 的 sparkline 已被 12 颗心跳点 + 颜色徽章覆盖 | ~0.5 工程日 |
| **3. 用户基数 / 部署形态变化后变成刚需**（条件触发） | | | | |
| | F8 WebView2 multi-profile | 🟦 条件刚需 | 用户当前只 1 个环境，可不做；**一旦加 staging / prod 第二个环境**，session 串台是必现 BUG | ~3-4 工程日 |
| | F13 MSIX + Winget 分发 | 🟦 条件刚需 | 仅当面向公众发布时——SmartScreen 阻塞 / "没签名的 exe 怎么装"是刚需 | ~3-4 工程日 |
| | F10 多语言扩充 + 即时切换 | 🟦 条件刚需 | 当前 zh-CN + en 已经覆盖目标用户；**当国际用户进来才需要** | ~5-7 工程日 |
| **4. Power-user 改进**（非刚需，做了好用） | | | | |
| | F9 命令面板 (Ctrl+K) | 🟩 改进 | 多两次点击而已——绝大多数人不会用 | ~2-3 工程日 |
| | F18 Cloudflare PoP 标识 | 🟩 改进 | hover tooltip 多一行字，但能让"为什么我这里慢"一眼可见 | ~0.5 工程日 |
| | F11 Explorer "Send to OpenClaw" | 🟩 改进 | 用户拖 / 复制路径自己粘贴——能凑合。**且依赖上游 attach_file 命令**，盲做风险 | ~5 工程日 + 上游协调 |
| | F15 WebView2 console 镜像到日志 | 🟩 改进 | 调试 bridge 注入的 JS 时有用，普通用户用不到 | ~1-2 工程日 |
| | F12 WebView2 进程资源监控 | 🟩 改进 | 任务管理器已经能看，重复造轮子 | ~1 工程日 |
| **5. 谨慎评估 / 看反馈再决定** | | | | |
| | F7 凭据 → Credential Manager | 🟪 待评估 | 当前 user-data folder 加密在 Edge SQLite 里，**没有具体的攻击场景**说明现状不安全。除非有审计需求或多用户机器场景，做了不增量价值 | ~5-7 工程日 |
| | F14 语音输入（Push-to-Talk） | 🟪 待评估 | 看用户基数中有多少人真用语音；先收集呼声 | ~5 工程日 |
| | F19 浮窗 AI 状态卡片 | 🟪 待评估 | F6 紧凑模式可能已经覆盖；做完发现重复 | ~3 工程日 |
| | F16 `openclaw://` URL scheme | 🟪 待评估 | 没有现成调用方需求；做完没人用 | ~1-2 工程日 |
| | F17 自定义状态栏字段 | 🟪 待评估 | 过度配置——多数人接受默认；增加 settings 复杂度 | ~3 工程日 |
| | F20 会话本地缓存 | 🟪 待评估 | **和 README "not offline-capable" 设计原则有张力**；做之前先明确这是"备份"而非"离线模式" | ~5-7 工程日 |
| **不建议做** | | | | |
| | F3 原方案 sparkline | ⬛ 不做 | 12 颗心跳点已经是 sparkline；信息密度过载——见上一轮讨论 | — |

> 并行于阶段 0-2 的**底线工作**（F21-F24 安全/可访问性/合规）：屏幕阅读器审计、高对比度主题、隐私白皮书、数据擦除选项——单独立项，不阻塞功能 release。

---

## 2. 分阶段路线图

### v3.1（4-5 周，~10 工程日）— "让壳立得住"

```
[ ] F1  Tray + Global Hotkey                 ~5d
[ ] F4  Velopack auto-update                 ~3-4d
[ ] F3-revised  Latency hover tooltip        ~0.5d
[ ] Settings: 新建 "Shortcuts" 面板（F1 配置项）
[ ] README "Features" 章节更新
```

完成后效果：装一次永远是新版、随时召唤、不再是"另一个 alt-tab 窗口"。**这是 native shell 的最小价值定义。**

### v3.2（4-5 周，~6-8 工程日）— "长任务工作流"

```
[ ] F6  Compact / Always-on-top               ~3d
[ ] F2  Toast on AI finish                    ~3d
[ ] F5  Diagnostic bundle export              ~2d  (如 v3.1 期间已有人报 bug 则提前)
[ ] Settings: 新建 "Notifications" 面板
```

完成后效果：开机挂角落 + 长任务跑完弹通知 + 出问题能一键导出现场。

### v3.3+（按需触发，每个独立 PR）

仅当具体触发条件满足时启动：

```
触发：用户增加第 2 个环境
[ ] F8  WebView2 multi-profile + 删除 ~180 行 recreation 状态机

触发：面向公众分发 / 上 GitHub Releases SmartScreen 抱怨
[ ] F13  MSIX 打包 + 代码签名 + Winget manifest

触发：英 / 中之外的用户呼声
[ ] F10  追加 ja-JP / es-ES / fr-FR 等

触发：bridge 兼容性问题（上游 OpenClaw Control UI 改了 DOM）
[ ] V1 #32  把关键字抽到 version-pinned JSON

触发：社区 issue 中"我想让 X 触发 OpenClaw"
[ ] F11 / F16  Explorer 集成 + URL scheme
```

### 平行底线（不阻塞功能 PR）

```
[ ] F21 屏幕阅读器审计（Accessibility Insights 跑一遍）
[ ] F22 高对比度主题
[ ] F23 PRIVACY.md
[ ] F24 Settings → "Privacy"（retention 滑块 + 一键清空）
```

每两个版本（约 v3.2、v3.4）顺手做一项即可。

---

## 3. 决策依据：为什么这样分

| 决策 | 理由 |
|---|---|
| F1 + F4 提升到阶段 0 | 这两条直接决定"为什么不用浏览器" + "为什么不停留旧版"；其他都是增益 |
| F2 / F6 合并到阶段 1 | 都是"长任务工作流"的两面——F6 让用户能看，F2 让用户不用看；先做 F6 因为**视觉常驻是更确定的需求** |
| F5 / F3-revised 单独阶段 | 出问题才用，先不做不痛；但 F5 一旦需要就刚需，所以"准备好预算" |
| F7 / F8 / F10 / F13 拆到阶段 3 触发式 | 都是**条件刚需**——条件不满足时做了浪费；写明触发条件，避免拍脑袋启动 |
| 阶段 5 全部"待评估" | 没有具体用户证据支持的功能不该排进 roadmap，避免 sunk cost |
| F3 原方案不做 | 信息密度重复 + 没有可执行价值——上一轮已说明 |

---

## 4. 功能目录（按 F 编号详述，仅供查阅）

> 排列顺序仅按 F 编号，**不代表优先级**。优先级看 §1 的刚需梯度。

### F1-F6 工作流类

### F1. System Tray + 全局快捷键召唤

**当前问题**：用户在 Word / Code / 浏览器里写东西时，要切到 OpenClaw 必须 Alt-Tab 找到这个窗口。Web 端浏览器 tab 就更别提了——埋在 N 个 tab 之间。

**建议**：

- 系统托盘图标，左键点击 = 显示/隐藏主窗口；右键菜单 = `Reload` / `Open Settings` / `View Logs` / `Quit` + 显示当前 `WORK STATUS`（IDLE / LIVE）
- 全局快捷键（默认 `Ctrl+Alt+Space`，可在 Settings → 新建 "Shortcuts" 面板配置）：
  - 召唤 / 隐藏主窗口
  - 召唤+焦点直接进 Control UI 输入框（注入 JS focus()）
- 关闭按钮可配置为"最小化到托盘"还是"退出"

**实现要点**：
- 托盘：`H.NotifyIcon`（NuGet）或 WinUI3 的 `H.NotifyIcon.WinUI`（社区库，专门为 WinUI 3 写的）。WinAppSDK 1.8 自带的 toast/系统集成 API 也覆盖一部分。
- 全局快捷键：`Microsoft.UI.Input` 不直接支持全局热键，需要 P/Invoke `RegisterHotKey`（[user32](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey)）+ 一个隐藏 `MessageWindow` 接 `WM_HOTKEY`，可以放在 [Helpers/WindowFrameHelper.cs](src/OpenClaw/Helpers/WindowFrameHelper.cs) 的同一类型中。
- 配置项加到 `AppSettings`：`MinimizeToTray: bool`、`GlobalHotkey: string`（"Ctrl+Alt+Space" 形式）、`StartMinimized: bool`。

**为什么是第一档**：直接把"另一个待打开的应用"变成"随时可召唤的工具"。AI 助手类客户端最大的差异化点。

### F2. AI 完成时的 Windows Toast 通知

**当前问题**：用户提交一个长任务（如让 OpenClaw 生成长文档、调多个工具），切到别的应用干别的，回来看才知道完成了没。

**建议**：

- 已经有 `WORK STATUS` 状态机（`MainViewModel.Formatting.cs` 的 `FormatWorkStatus` 区分 LIVE / IDLE / WAIT）
- 在 `OnControlUiSnapshotUpdated` 中检测 `LIVE → IDLE` 跃迁（且持续 ≥ 1.5 s 防抖）
- 用 `Microsoft.Toolkit.Uwp.Notifications` 或 WinAppSDK 1.7+ 的 `AppNotificationManager` 发 toast：
  - 标题：`OpenClaw — 任务完成`
  - 副标题：当前模型名（已经在 `ModelSummaryText`）
  - 操作按钮：`查看` → 召唤主窗口 + focus chat
- Settings → DevTools 加开关 `Notify when AI finishes`

**配套**：
- Auth Required / Pairing Required 也用 toast（区分严重程度），点击直接定位到 Settings → Sessions
- 任务**开始** 5 分钟还没完成 → 选项：发"长任务运行中"提示（避免每个 `>` 都打扰）

**为什么是第一档**：原生通知是 web 给不了的核心体验，配合 F1 托盘构成完整工作流。

### F3. 延迟/状态历史时序图（sparkline）

**当前问题**：右上角延迟徽章只显示瞬时值——用户看不出"刚才掉了一下、现在恢复了"还是"持续在退化"。

**建议**：

- `ControlUiLatencyService` 已经每 3 s 一次 HEAD probe，在内存里 buffer 最近 60 个样本（3 分钟）就够了
- 在状态栏右侧（`StatusBarSurface`）添加一个 60 px 宽 12 px 高的 sparkline polyline，颜色随时延映射（绿 / 黄 / 红，与 `FormatLatencySummary` 的 brush 一致）
- hover 显示完整数值序列 + 平均/p95/最大值
- 点击 → 弹出"连接历史"对话框（图表区扩到 12 小时 / 24 小时 / 7 天，需要把 ring buffer 持久化到 `%LOCALAPPDATA%\OpenClaw\latency-history.bin`，定长 record）

**实现要点**：
- WinUI 3 `Polyline`（`Microsoft.UI.Xaml.Shapes.Polyline`）即可，不需要 chart 库
- 数据流：`ControlUiLatencyService.LatencyUpdated` → `MainViewModel` 一个新的 `ObservableCollection<double>`，绑定到 polyline 的 `Points` 转换器
- 持久化先不上，做一个 in-memory 版本即可

**为什么是第一档**：这是 Cloudflare Tunnel 用户最关心的实时数据；现在的徽章浪费了已经在采集的数据。

### F4. 自动检查更新

**当前问题**：v3.0.0 → v3.0.6 在两周内发 7 个版本——用户每次都要手动去 GitHub Release 下载替换。

**建议**：

- 用 [Velopack](https://velopack.io/) 或 [Squirrel.Windows](https://github.com/Squirrel/Squirrel.Windows) 集成
- 启动时（异步、不阻塞 UI）调一次 `https://api.github.com/repos/<owner>/<repo>/releases/latest`，比对 `AppMetadata.GetDisplayVersion()`，有新版本时在 InfoBar / 标题栏角标提示
- 用户点击"立即更新"→ Velopack 走"先下载到 packages 目录、退出时用 update.exe 应用、重启"标准流程
- Settings → DevTools 加：`Auto-check on launch`（默认 on）/`Auto-download in background`（默认 off）/`Last checked: ...`

**实现要点**：
- 项目目前是 Unpackaged 自包含（`<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`），Velopack 完美匹配（它本来就是非 MSIX 设计）
- 当前 csproj 已经 pin 版本（V2 #11 完成），auto-update 不会冲到一个 floating 解析问题

**为什么是第一档**：发版节奏快 + 自动更新天然减少摩擦。这一项还会让用户更愿意装这个壳子（"装一次，永远是新版"）。

### F5. 诊断报告一键打包

**当前问题**：用户报 bug 时要：截图日志窗口 + 翻 settings.json + 找 WebView2 console 输出——没人会做，bug 报告都是"它就是不工作"。

**建议**：

- 在 Settings → DevTools 加按钮"导出诊断包"
- 收集：
  1. 最近 7 天的 `openclaw-YYYY-MM-DD.log`（已经有 14 天 retention）
  2. `settings.json` 副本（**自动 redact**：`gatewayUrl` 中的 host 替换为 `<host>`、移除任何看起来像 token 的字符串）
  3. 当前 `RecoveryTelemetrySnapshot`（已经在 `ShellSessionCoordinator` 暴露）
  4. 系统信息：Windows 版本、WebView2 runtime 版本、.NET 版本、屏幕缩放
  5. 最近一次 Diagnostic 报告
- 打包成 `openclaw-diag-YYYY-MM-DD-HHMM.zip`，放桌面，并把路径复制到剪贴板
- 弹 InfoBar 提示用户"已生成 / 请检查文件后再发送"

**为什么是第一档**：从 V1 #20 / V2 §3 一直被标"测试覆盖空白"——但也包括"用户支持成本"。这一项让诊断从"我猜"变成"我看证据"。

### F6. 紧凑模式 (Compact / Always-on-Top)

**当前问题**：用户在主屏写代码、想让 OpenClaw 视野中显示状态/小窗持续可见——必须并排放，两个窗口手动调大小。

**建议**：

- 标题栏增加一个按钮 "📌"（钉住）
- 第一段功能：`Always on top`（用 `AppWindow.Presenter` `OverlappedPresenter.IsAlwaysOnTop = true`）
- 第二段功能：`Compact mode`（窗口尺寸缩到 360×120，隐藏 WebView，仅显示状态条 + 一个"全屏返回"按钮）
- 紧凑模式下保留：心跳/延迟徽章、`WORK STATUS`、当前模型；点击模型名 → 展开回正常模式
- 双击托盘也可以切换紧凑模式

**实现要点**：
- 已经有 `AppWindow.Resize` 和 `AppWindow.Move`（[MainWindow.Lifecycle.cs:23,28](src/OpenClaw/MainWindow.Lifecycle.cs#L23-L28)）
- 紧凑模式记忆窗口位置 / 普通模式记忆位置——分别存 `windowWidth` 和 `compactWindowLeft / Top`
- 从紧凑模式切回大模式时，恢复用户原来的尺寸

**为什么是第一档**：和 F1 / F2 / F3 配合形成"开机后挂在屏幕角落 + 任务完成弹通知"的工作流，这是远端 AI 助手最自然的使用方式。

---

### F7-F12 扩展类

### F7. 凭据安全升级——Windows Credential Manager 集成

**当前问题**：OpenClaw Gateway 的 token / device pairing token 现在完全靠 WebView2 的 cookie / localStorage 存储——Edge 加密在 SQLite 里，但 user data folder 一旦泄露（备份、磁盘镜像）就能离线导出。

**建议**：

- Settings → 新增 "Credentials" 面板
- 用户手动粘贴 Gateway shared token 后，存到 [Windows Credential Manager](https://learn.microsoft.com/windows/win32/api/wincred/) （`CredWriteW` P/Invoke 或 `Microsoft.Toolkit.Uwp.Helpers` 的 PasswordVault）
- Bridge 注入一段 JS：检测到 Control UI 处于 `auth_required` 阶段时，把 token 自动注入到 input + 提交（用 `HostedUiBridge.SendCommandAsync("auth_inject")`，需要上游 Control UI 暴露相关 hook，或 fallback 到 V1 #28 风格的 input 注入但限定为 token field）
- 对应 `gatewayUrl` 多环境 → token 也按环境分别存
- 解锁可选：使用 Windows Hello / PIN 二次确认（`Windows.Security.Credentials.UI.UserConsentVerifier`）

**为什么是第二档**：用户当前只有一个环境，token 只手输一次的频率不高；但对**多设备 / 多环境** 用户一旦上来就是必备。也是为 F8 多环境铺垫。

### F8. WebView2 真正的多环境隔离（Multi-Profile API）

**V1 #1 复用**：项目设计了"多环境"但只能切目录名，进程级 user-data folder 的限制让真隔离失效（V1 详述）。

**建议**：

- 升级到 `CoreWebView2ControllerOptions` API（WebView2 1.0.2592+，WinAppSDK 1.8 已经满足），每个环境用不同的 `ProfileName`
- Settings → Environments 增加每环境的 `Profile name` 字段（默认 = `Sanitize(name)`）
- 删掉 `MainWindow.WebView.cs` 整个 ~180 行 recreation 状态机——切环境只需要 `coreWebView.Profile = newProfile`，零闪屏

**配套功能**：
- 每环境支持独立的 user-agent（适配某些 Cloudflare Access 规则）
- 每环境独立的 `__openclaw/control-ui-config.json` 缓存（已经被 latency probe 用作探测端点，这里可以扩展为"配置缓存"，离线时显示上次的状态）

**为什么是第二档**：需求来自"多设备 / 多环境"，单环境用户暂时不痛。但做了之后 F7 凭据按环境隔离才合理。

### F9. 命令面板 (Ctrl+K)

**建议**：

- 全局 `Ctrl+K`（在主窗口聚焦时）打开半透明命令面板（VS Code 风格）
- 命令清单（来自 `MainViewModel.Commands.*`）：
  - `Reload Page`
  - `Open Dev Tools`
  - `Run Diagnostics`
  - `Switch Environment → <env>`
  - `Toggle Compact Mode`
  - `Toggle Always on Top`
  - `Open Logs Folder`
  - `Export Diagnostic Bundle`（F5）
  - `Clear Session Data`
- 显式 fuzzy match（[FuzzySharp](https://www.nuget.org/packages/FuzzySharp/) 或者手写 Levenshtein-like）

**实现要点**：
- 浮层用 `ContentDialog` 全屏 + 自定义 visualstate + 顶部 `AutoSuggestBox`，下方 `ListView`
- 命令注册集中到一个 `IShellCommandRegistry`，每个命令带 `Id`、`Name`、`Keywords`、`CanExecute`、`Execute`

**为什么是第二档**：power-user 福音，但不是普通用户的痛点。配合 F1 / F6 的快捷键体系一起做，工作量分摊。

### F10. 多语言扩充 + 即时切换

**当前问题**：仅支持英文 + 简中，切换需要重启（README v2.1.2 提到的 hint）。

**建议**：

- 加：日文 (ja-JP)、韩文 (ko-KR)、繁中 (zh-TW)、西语 (es-ES)、法语 (fr-FR) 的 .resw 文件
- 即时切换：通过把所有面板的 `Text="{x:Bind helpers:StringResources.X}"` 改成 OneWay 绑定到一个 `LanguageVersion` 计数器，切语言时 increment 计数器触发重新读 resw（需要把 `StringResources.Get(key)` 改成 indexer property `[string key]`，确保支持 `INotifyPropertyChanged` 风格）
- 或者更简单：切换后弹"应用语言"按钮 → 重建 MainWindow（保留 ViewModel，重新 `InitializeComponent`），无需进程重启

**实现要点**：
- 翻译可以走半自动：用 OpenClaw 自己的 LLM 跑一次初稿，再人工 review。每个 .resw ~120 字符串，翻译成本不高
- 注意 RTL（阿语）暂不做，但加资源时考虑 `FlowDirection`

**为什么是第二档**：扩用户面，但不影响 power-user 的日常。

### F11. 右键 Explorer "Send to OpenClaw"

**建议**：

- 注册 Windows shell context menu（仅文件 / 文件夹）："Send to OpenClaw"
- 用户在 Explorer 选中一个文件 / 文件夹 → 右键 → 选项 → 自定义 verb → 启动 OpenClaw（`openclaw://send?path=<encoded>`）
- 主窗口收到 `OnLaunched` 的 protocol activation → 召唤 + 通过 bridge 给 Control UI 发 `host_command("attach_file", { path })`，由上游处理（**前提是上游 OpenClaw 暴露了 attach_file 命令**——目前 bridge 已有通用 `dispatchBridgeEvent` 路径）

**实现要点**：
- 注册自定义 URL scheme（`openclaw://`）：在 `Package.appxmanifest` 加 `<uap:Extension Category="windows.protocol">` 即可（unpackaged 也可以通过注册表，但 MSIX 更简单——见 F13）
- shell context menu 的 unpackaged 实现需要 .reg 文件 + COM server，复杂度上来了；MSIX 用 `windows.fileExplorerContextMenus` 简单很多

**为什么是第二档**：依赖上游 attach_file 命令、依赖 MSIX 打包。属于"做了之后用户惊叹"的级别。

### F12. WebView2 进程资源监控

**建议**：

- Diagnostics 报告 + 状态栏 hover tooltip 显示当前 WebView2 进程的 CPU / 内存
- `coreWebView.BrowserProcessId` 拿到 PID → `Process.GetProcessById` 读 `WorkingSet64` / `TotalProcessorTime`
- 异常情况（内存 > 1 GB 或 CPU 持续 > 50%）状态栏闪红，提示"考虑 Reload"

**为什么是第二档**：诊断深度。不解决日常使用，但帮助理解"为什么 OpenClaw 卡了"。

---

### F13-F20 特定场景类

### F13. MSIX 打包 + Winget 分发

- 当前是 Unpackaged self-contained，部署需要"下载 zip → 解压 → 双击 exe"
- 改 MSIX 后：双击 .msix 安装、自动注册 protocol scheme（F11 依赖）、上 Microsoft Store 与 Winget
- `winget install OpenClaw.Manager` 一行完成
- 复杂度：中等。需要 code signing 证书（Microsoft Store 免费、企业证书几十美元/年）

### F14. 语音输入 (Push-to-Talk)

- 输入框旁边一个麦克风按钮，按住说话 → Windows speech recognition (`System.Speech.Recognition.SpeechRecognitionEngine` / Windows.Media.SpeechRecognition)
- 转文字后注入 Control UI 输入框
- 适配语言跟随 `AppLanguage`

### F15. WebView2 控制台镜像到日志

- `coreWebView.WebMessageReceived` 已经在用；新增 `coreWebView.DevToolsProtocolEventReceived` 订阅 `Runtime.consoleAPICalled`
- 把 Control UI 的 `console.log` / `console.error` 写进 `openclaw-YYYY-MM-DD.log`（带 prefix `[ControlUI]`）
- Settings → DevTools 加开关 `Mirror Control UI console`

### F16. OpenClaw `chat:` URL scheme deep link

- `openclaw://chat?msg=...&model=...` → 打开应用、定位到指定环境、注入消息
- 配合 F11，可以让其他应用一键问 AI

### F17. 自定义状态栏字段

- Settings → 新建 "Status Bar" 面板
- 让用户勾选要显示的字段（HB / MODEL / AUTH / WORK / LATENCY / PoP / GATEWAY URL）
- 字段顺序拖拽

### F18. Cloudflare Tunnel 元数据展示

- 已经从 `cf-ray` 头知道经过 Cloudflare（latency probe 里）
- 进一步 parse `cf-ray: <random>-XXX` 的 `XXX`（colocation 代码，如 `LAX` / `HKG` / `SIN`），展示在 latency 徽章 hover tooltip 里
- `cf-ray` 还能算出 anycast 路径变化（跨 PoP 切换 → 提示"Cloudflare 路由切换中"）

### F19. 主屏幕"AI 状态卡片"小组件

- WinUI 3 没有原生 widget API，但可以做一个独立的 always-on-top 透明小窗（120×80）
- 显示：环境名 + WORK 状态 + 心跳点（脉动）
- 双击 → 召唤主窗口
- 类似 macOS Dynamic Island 的精神

### F20. 会话历史本地缓存（只读）

- bridge 监听 `openclaw-session-ready` + 后续消息事件
- 把消息序列写到 `%LOCALAPPDATA%\OpenClaw\sessions\<env>\<date>.jsonl`
- Settings → 新建 "History" 面板浏览本地缓存（**注意**：这与 README "This project is not... offline-capable" 微妙——应明确这是"only readable backup"而非"replay")
- 离线时也能 `Ctrl+F` 搜索历史 → 在缓存里命中

---

## 5. 安全 / 可访问性 / 合规（必须做、低 visibility）

### F21. 屏幕阅读器审计

- 所有 button 加 `AutomationProperties.Name`
- 状态栏指示灯 + 心跳点要可被读屏（`AutomationProperties.LiveSetting="Polite"`）
- 命令面板 (F9) 必须键盘可达
- Microsoft Inclusive Design Toolkit 的 Accessibility Insights 工具跑一遍

### F22. 高对比度主题

- 已有 Light / Dark / System；加 High Contrast（跟随系统 `Windows.UI.ViewManagement.AccessibilitySettings.HighContrast`）

### F23. 隐私白皮书

- README 加 PRIVACY.md：声明本应用**不发送遥测**到任何第三方、所有数据都在本地、与 OpenClaw Gateway 之间的流量完全由用户控制
- 自动更新 (F4) 唯一的外发是 GitHub API（也要在文档说明）

### F24. 保留期 / 数据擦除选项

- Settings → "Privacy"：滑块控制日志 retention（默认 14 天，可调到 7 / 30 / 永久）
- 一键"清除所有本地数据"（settings.json + logs + WebView2Data + sessions/）

---

## 6. 不建议做的

为了帮助裁剪：

- ❌ **离线模式 / 本地 Gateway**：违反 README 设计原则
- ❌ **嵌入 OpenClaw 全部前端代码到 native 端**：维护负担太大，与上游迭代脱节
- ❌ **多 Gateway 同时连接 / federation**：单用户 / 单 VPS 用户没需求，复杂度爆炸
- ❌ **企业级管理控制台 (admin UI)**：上游 `/_admin/` 已经覆盖，原生 shell 没必要复制
- ❌ **完整 chat UI 重写**：Lit/Vite 上游就行，原生壳的价值在"环境壳"层
- ❌ **跨平台 (macOS / Linux)**：那是另一个项目，不是"WinUI 3 shell"

---

## 7. v3.0.6 的小遗留事项

review 这一轮代码时顺手记的（不属于"新功能"，但属于"清理收口"）：

1. **`App.xaml.cs:30, 35`** —— 静态 `Logger` 和 `Configuration` 字段初始化器仍然在 type-load 时 new()。如果将来要做 F4 自动更新（外置 update.exe 启动器），field initializer 的失败处理会比构造函数里复杂；考虑改成 lazy + DI（V1 #7 长期方向）。
2. **`OpenClaw.Core` 拆出来后 [OpenClaw.csproj:33-58](src/OpenClaw/OpenClaw.csproj#L33-L58)** 用 `<Compile Remove>` 来剥离迁走的文件——能 work，但 26 个 `<Compile Remove>` 行很容易漏一个就引入"两份同名编译单元"。建议改成把这些文件**真的物理删除/移动**到 `OpenClaw.Core` 项目下（git mv），csproj 里就不需要 Remove 列表了。
3. **`README.md` 的 "Project Structure" 段**还显示老结构（只有 `src/OpenClaw/`，没提 `src/OpenClaw.Core/`），需要更新。
4. **测试覆盖** —— V1 #20 / V2 §3 列的"`NormalizeSettings` legacy 迁移、`BuildEnvironmentFolderName` sanitize、`SettingsViewModel.SaveAll` validation"三块纯函数 unit test 仍未补；现在 `OpenClaw.Core` 拆出来后，`ConfigurationService` / `WebViewService.BuildEnvironmentFolderName` 都可以直接放进 `tests/OpenClaw.Tests` 了，写起来比 V1 时更便宜。
5. **`Directory.Build.props`**：上一轮删了 `RestorePackagesConfig`，但还可以加 `<EnableNETAnalyzers>true</EnableNETAnalyzers>` + `<AnalysisMode>Recommended</AnalysisMode>`，现在 `IAppLogger` / `Core` 项目刚拆开是加分析器的好时机（V1 #29）。

---

review 范围：v3.0.6 当前 src/ 与 tests/ 全部源码、`%LOCALAPPDATA%\OpenClaw\settings.json` 实际部署、README 与 changelog。重点关注"项目下一步能做什么用户可见的功能"，因此弱化继续追讨 V1 / V2 已经覆盖的代码质量项。
